using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Provisions DNS (OctoDNS via Docker), ACA managed certificate binding, and GitHub Actions variables.
/// </summary>
public sealed class DomainProvisioner
{
    private readonly IProcessRunner _processRunner;
    private readonly IAzureContainerAppReader _azureReader;
    private readonly DnsRecordPlanner _planner;
    private readonly OctoDnsZoneWriter _zoneWriter;
    private readonly OctoDnsConfigWriter _configWriter;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync;

    public DomainProvisioner(
        IProcessRunner processRunner,
        IAzureContainerAppReader azureReader,
        DnsRecordPlanner? planner = null,
        OctoDnsZoneWriter? zoneWriter = null,
        OctoDnsConfigWriter? configWriter = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _azureReader = azureReader ?? throw new ArgumentNullException(nameof(azureReader));
        _planner = planner ?? new DnsRecordPlanner();
        _zoneWriter = zoneWriter ?? new OctoDnsZoneWriter();
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

        var targets = await _azureReader.GetTargetsAsync(
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
        _zoneWriter.WriteToDirectory(plan, options.OctoDnsZoneDirectory);

        var (mountRoot, configInContainer, zonesRelative) = ResolveDockerMount(
            options.OctoDnsConfigPath,
            options.OctoDnsZoneDirectory);

        _configWriter.WriteToFile(
            provider,
            [plan.ZoneName],
            Path.GetFullPath(options.OctoDnsConfigPath),
            zonesRelative);

        await RunOctoDnsDockerAsync(provider, options, mountRoot, configInContainer, cancellationToken)
            .ConfigureAwait(false);

        await WaitForDnsAsync(plan, options, cancellationToken).ConfigureAwait(false);

        var certificateName = string.IsNullOrWhiteSpace(options.ManagedCertificateName)
            ? SanitizeCertificateName(customHostname)
            : options.ManagedCertificateName!;

        await RunRequiredAsync(
            "az",
            [
                "containerapp", "hostname", "add",
                "--hostname", customHostname,
                "-g", targets.ResourceGroup,
                "-n", targets.ContainerAppName
            ],
            cancellationToken,
            "hostname add").ConfigureAwait(false);

        var validationMethod = plan.Kind == HostnameKind.Apex ? "HTTP" : "CNAME";

        await RunRequiredAsync(
            "az",
            [
                "containerapp", "hostname", "bind",
                "--hostname", customHostname,
                "-g", targets.ResourceGroup,
                "-n", targets.ContainerAppName,
                "--environment", targets.EnvironmentName,
                "--validation-method", validationMethod
            ],
            cancellationToken,
            "hostname bind").ConfigureAwait(false);

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

    private async Task RunOctoDnsDockerAsync(
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        string mountRoot,
        string configInContainer,
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
        args.Add("octodns-sync");
        args.Add("--config-file");
        args.Add(configInContainer);
        args.Add("--doit");

        await RunRequiredAsync("docker", args, cancellationToken, "OctoDNS docker sync", env)
            .ConfigureAwait(false);
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
