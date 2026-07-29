using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Provisions DNS (OctoDNS), ACA managed certificate binding, and GitHub Actions variables.
/// </summary>
public sealed class DomainProvisioner
{
    private readonly IProcessRunner _processRunner;
    private readonly IAzureContainerAppReader _azureReader;
    private readonly DnsRecordPlanner _planner;
    private readonly OctoDnsZoneWriter _zoneWriter;
    private readonly Func<TimeSpan, CancellationToken, Task>? _delayAsync;

    public DomainProvisioner(
        IProcessRunner processRunner,
        IAzureContainerAppReader azureReader,
        DnsRecordPlanner? planner = null,
        OctoDnsZoneWriter? zoneWriter = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _azureReader = azureReader ?? throw new ArgumentNullException(nameof(azureReader));
        _planner = planner ?? new DnsRecordPlanner();
        _zoneWriter = zoneWriter ?? new OctoDnsZoneWriter();
        _delayAsync = delayAsync;
    }

    public async Task<string> ProvisionAsync(
        string customHostname,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customHostname);
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

        await RunRequiredAsync(
            "octodns-sync",
            ["--config-file", options.OctoDnsConfigPath, "--doit"],
            cancellationToken,
            "OctoDNS sync").ConfigureAwait(false);

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

    private async Task WaitForDnsAsync(DnsPlan plan, AzureCustomDomainOpsOptions options, CancellationToken cancellationToken)
    {
        // Lightweight readiness: ensure at least one planned record type was written; real DNS poll is best-effort via dig/nslookup when available.
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

        // Do not hard-fail solely on nslookup; DigiCert validation may still succeed after bind.
    }

    private async Task RunRequiredAsync(
        string fileName,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken,
        string operationName)
    {
        var result = await _processRunner.RunAsync(fileName, args, cancellationToken).ConfigureAwait(false);
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
