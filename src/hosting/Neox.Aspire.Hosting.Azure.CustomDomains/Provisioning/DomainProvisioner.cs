using System.Globalization;
using System.Text.RegularExpressions;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Provisions DNS (OctoDNS via Docker, upsert-only), ACA managed certificate binding, and GitHub Actions variables.
/// </summary>
public sealed class DomainProvisioner
{
    private static readonly Regex DeletesCount = new(
        @"Deletes=(\d+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IProcessRunner _processRunner;
    private readonly IAzureContainerAppClient _azureClient;
    private readonly DnsRecordPlanner _planner;
    private readonly OctoDnsZoneUpserter _zoneUpserter;
    private readonly OctoDnsConfigWriter _configWriter;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync;

    public DomainProvisioner(
        IProcessRunner processRunner,
        IAzureContainerAppClient azureClient,
        DnsRecordPlanner? planner = null,
        OctoDnsZoneUpserter? zoneUpserter = null,
        OctoDnsConfigWriter? configWriter = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _azureClient = azureClient ?? throw new ArgumentNullException(nameof(azureClient));
        _planner = planner ?? new DnsRecordPlanner();
        _zoneUpserter = zoneUpserter ?? new OctoDnsZoneUpserter();
        _configWriter = configWriter ?? new OctoDnsConfigWriter();
        _delayAsync = delayAsync;
    }

    public async Task<string> ProvisionAsync(
        string customHostname,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customHostname);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var appName = options.ContainerAppResourceName
            ?? throw new InvalidOperationException("ContainerAppResourceName must be set.");

        var targets = await _azureClient.GetTargetsAsync(
            appName,
            resourceGroup: Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
            environmentName: options.ContainerAppEnvironmentName,
            cancellationToken).ConfigureAwait(false);

        var planInput = new DnsPlanInput(
            customHostname,
            targets.Fqdn,
            targets.StaticIp,
            targets.CustomDomainVerificationId);

        var plan = _planner.Plan(planInput);

        var (mountRoot, configInContainer, zonesRelative) = ResolveDockerMount(
            options.OctoDnsConfigPath,
            options.OctoDnsZoneDirectory);

        _configWriter.WriteToFile(
            provider,
            [plan.ZoneName],
            Path.GetFullPath(options.OctoDnsConfigPath),
            zonesRelative);

        // Best-effort dump of the live zone so upsert starts from existing records (greenfield OK if dump fails).
        await TryDumpZoneAsync(provider, options, mountRoot, configInContainer, zonesRelative, plan.ZoneName, cancellationToken)
            .ConfigureAwait(false);

        _zoneUpserter.UpsertToDirectory(plan, options.OctoDnsZoneDirectory);

        var dryRun = await RunOctoDnsDockerAsync(
                provider,
                options,
                mountRoot,
                [
                    "octodns-sync",
                    "--config-file",
                    configInContainer
                ],
                required: true,
                operationName: "OctoDNS docker sync dry-run",
                cancellationToken)
            .ConfigureAwait(false);

        EnsureUpsertOnlyPlan(dryRun.StandardOutput + Environment.NewLine + dryRun.StandardError);

        await RunOctoDnsDockerAsync(
                provider,
                options,
                mountRoot,
                [
                    "octodns-sync",
                    "--config-file",
                    configInContainer,
                    "--doit"
                ],
                required: true,
                operationName: "OctoDNS docker sync apply",
                cancellationToken)
            .ConfigureAwait(false);

        await WaitForDnsAsync(plan, options, cancellationToken).ConfigureAwait(false);

        var certificateName = string.IsNullOrWhiteSpace(options.ManagedCertificateName)
            ? SanitizeCertificateName(customHostname)
            : options.ManagedCertificateName!;

        var validationMethod = plan.Kind == HostnameKind.Apex ? "HTTP" : "CNAME";

        await _azureClient.BindManagedHostnameAsync(
                targets,
                customHostname,
                certificateName,
                validationMethod,
                cancellationToken)
            .ConfigureAwait(false);

        await RunRequiredAsync(
            "gh",
            [
                "variable", "set", options.CertificateGitHubVariableName,
                "--body", certificateName
            ],
            cancellationToken,
            "gh variable set").ConfigureAwait(false);

        return certificateName;
    }

    /// <summary>
    /// Ensures the OctoDNS plan contains no Deletes (DomainOps upsert-only invariant).
    /// </summary>
    internal static void EnsureUpsertOnlyPlan(string syncOutput)
    {
        var matches = DeletesCount.Matches(syncOutput);
        foreach (Match match in matches)
        {
            var deletes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (deletes > 0)
            {
                throw new InvalidOperationException(
                    "OctoDNS plan includes Deletes, which violates the DomainOps upsert-only invariant. " +
                    "Refusing to apply." + Environment.NewLine + syncOutput);
            }
        }

        if (matches.Count == 0 &&
            !syncOutput.Contains("No changes were planned", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Could not verify OctoDNS upsert-only plan (missing Deletes summary). " +
                "Refusing to apply." + Environment.NewLine + syncOutput);
        }
    }

    private async Task TryDumpZoneAsync(
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string mountRoot,
        string configInContainer,
        string zonesRelative,
        string zoneName,
        CancellationToken cancellationToken)
    {
        var zoneFqdn = zoneName.Trim().TrimEnd('.') + ".";

        // Dump failures (missing zone, auth edge cases) leave an empty base; upsert still creates ACA records.
        await RunOctoDnsDockerAsync(
                provider,
                options,
                mountRoot,
                [
                    "octodns-dump",
                    "--config-file",
                    configInContainer,
                    "--output-dir",
                    zonesRelative,
                    zoneFqdn,
                    provider.Name,
                    "--lenient"
                ],
                required: false,
                operationName: "OctoDNS docker dump",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ProcessResult> RunOctoDnsDockerAsync(
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string mountRoot,
        IReadOnlyList<string> octodnsArgs,
        bool required,
        string operationName,
        CancellationToken cancellationToken)
    {
        var image = string.IsNullOrWhiteSpace(options.OctoDnsDockerImage)
            ? provider.DefaultDockerImage
            : options.OctoDnsDockerImage!;

        var args = new List<string>
        {
            "run", "--rm",
            "-v", $"{mountRoot}:/octodns",
            "-w", "/octodns"
        };

        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (yamlProperty, parameter) in provider.AuthParameters)
        {
            if (!provider.AuthEnvBindings.TryGetValue(yamlProperty, out var envVar))
            {
                continue;
            }

            var value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Provider auth parameter '{parameter.Name}' is empty. Set Parameters__{parameter.Name}.");
            }

            // Pass name only so the secret is not present on the docker argv; value comes from process env.
            args.Add("-e");
            args.Add(envVar);
            env[envVar] = value;
        }

        args.Add(image);
        args.AddRange(octodnsArgs);

        var result = await _processRunner.RunAsync("docker", args, cancellationToken, environment: env)
            .ConfigureAwait(false);

        if (required && !result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{operationName} failed ({result.ExitCode}): {result.StandardError}{Environment.NewLine}{result.StandardOutput}");
        }

        return result;
    }

    /// <summary>
    /// Resolves the host directory to mount and container-relative paths for config + zones.
    /// </summary>
    internal static (string MountRoot, string ConfigInContainer, string ZonesRelativeToMount) ResolveDockerMount(
        string configPath,
        string zoneDirectory)
    {
        var configFull = Path.GetFullPath(configPath);
        var zonesFull = Path.GetFullPath(zoneDirectory);
        var configDir = Path.GetDirectoryName(configFull) ?? Directory.GetCurrentDirectory();

        var mountRoot = GetCommonRoot(configDir, zonesFull);
        var configInContainer = ToContainerRelative(mountRoot, configFull);
        var zonesRelative = "./" + ToContainerRelative(mountRoot, zonesFull).Replace('\\', '/');

        return (mountRoot, configInContainer.Replace('\\', '/'), zonesRelative);
    }

    private static string GetCommonRoot(string pathA, string pathB)
    {
        var a = Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var b = Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var partsA = a.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var partsB = b.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var len = Math.Min(partsA.Length, partsB.Length);
        var common = new List<string>();
        for (var i = 0; i < len; i++)
        {
            if (!string.Equals(partsA[i], partsB[i], StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            common.Add(partsA[i]);
        }

        if (common.Count == 0)
        {
            return Directory.GetCurrentDirectory();
        }

        // On Windows, first segment may be "C:" — Path.Combine handles it.
        return Path.Combine(common.ToArray());
    }

    private static string ToContainerRelative(string mountRoot, string fullPath)
    {
        var root = Path.GetFullPath(mountRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(fullPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path '{fullPath}' is not under Docker mount root '{mountRoot}'.");
        }

        var relative = Path.GetRelativePath(mountRoot, full);
        return relative.Replace('\\', '/');
    }

    private async Task WaitForDnsAsync(DnsPlan plan, AzureCustomDomainOpsOptions options, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + options.DnsPropagationTimeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var probe = plan.Records.FirstOrDefault(r => r.Type is "CNAME" or "A");
            if (probe is null)
            {
                return;
            }

            var host = string.IsNullOrEmpty(probe.Name) ? plan.ZoneName : $"{probe.Name}.{plan.ZoneName}";
            var lookup = await _processRunner.RunAsync(
                "nslookup",
                [host],
                cancellationToken).ConfigureAwait(false);

            if (lookup.Succeeded || lookup.StandardOutput.Contains(probe.Value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await DelayAsync(options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunRequiredAsync(
        string fileName,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken,
        string operationName,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var result = await _processRunner.RunAsync(fileName, args, cancellationToken, environment: environment)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"{operationName} failed ({result.ExitCode}): {result.StandardError}{Environment.NewLine}{result.StandardOutput}");
        }
    }

    private Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => _delayAsync is not null
            ? _delayAsync(delay, cancellationToken)
            : Task.Delay(delay, cancellationToken);

    internal static string SanitizeCertificateName(string hostname)
    {
        var sanitized = new string(hostname.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');

        if (sanitized.Length > 60)
        {
            sanitized = sanitized[..60].TrimEnd('-');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "aca-managed-cert" : sanitized;
    }
}
