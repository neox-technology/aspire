using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Coordinates custom domain verify / guard / provision pipeline actions.
/// </summary>
public sealed class DomainOpsOrchestrator
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;
    private readonly DnsRecordPlanner _planner;
    private readonly DnsRecordVerifier _verifier;
    private readonly OctoDnsZoneWriter _zoneWriter;

    public DomainOpsOrchestrator(IProcessRunner processRunner, ILogger logger)
        : this(processRunner, logger, new DnsRecordPlanner(), new DnsRecordVerifier(), new OctoDnsZoneWriter())
    {
    }

    public DomainOpsOrchestrator(
        IProcessRunner processRunner,
        ILogger logger,
        DnsRecordPlanner planner,
        DnsRecordVerifier verifier,
        OctoDnsZoneWriter zoneWriter)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _zoneWriter = zoneWriter ?? throw new ArgumentNullException(nameof(zoneWriter));
    }

    public async Task VerifyAsync(
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken,
        IReadOnlyList<DnsRecord>? observedRecords = null,
        DnsPlanInput? planInput = null)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(options);

        var hostname = await customDomain.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new InvalidOperationException("Custom domain parameter is empty. Set Parameters__customDomain.");
        }

        await GuardCoreAsync(certificateName, options, cancellationToken).ConfigureAwait(false);

        if (planInput is null)
        {
            planInput = TryBuildPlanInputFromEnvironment(hostname);
        }

        if (planInput is null)
        {
            _logger.LogInformation(
                "domain-verify: certificate/domain parameters OK for {Resource}; skipping DNS drift check (no ACA plan input).",
                targetResource.Name);
            return;
        }

        var plan = _planner.Plan(planInput);
        _logger.LogInformation(
            "domain-verify: planned {Count} DNS records for {Hostname} ({Kind}).",
            plan.Records.Count,
            hostname,
            plan.Kind);

        if (observedRecords is null)
        {
            return;
        }

        var drift = _verifier.FindDrift(plan.Records, observedRecords);
        if (drift.Count > 0)
        {
            throw new InvalidOperationException(
                "DNS drift detected:" + Environment.NewLine + string.Join(Environment.NewLine, drift));
        }

        _logger.LogInformation("domain-verify: DNS records match expected plan.");
    }

    public Task GuardAsync(
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
        => GuardCoreAsync(certificateName, options, cancellationToken);

    public Task ProvisionAsync(
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(options);
        _ = _processRunner;
        _ = _zoneWriter;

        throw new NotImplementedException(
            "domain-provision is not implemented yet. Use a package revision that includes DNS/cert provisioning.");
    }

    /// <summary>
    /// Plans records and writes OctoDNS zone YAML (used by provision and tests).
    /// </summary>
    public string WritePlannedZone(DnsPlanInput input, string zoneDirectory)
    {
        var plan = _planner.Plan(input);
        return _zoneWriter.WriteToDirectory(plan, zoneDirectory);
    }

    private async Task GuardCoreAsync(
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.RequireCertificateName)
        {
            _logger.LogInformation("domain-guard skipped because RequireCertificateName is false.");
            return;
        }

        var value = await certificateName.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Certificate name parameter is empty. Set Parameters__certificateName (or disable RequireCertificateName for bootstrap).");
        }

        _logger.LogInformation("domain-guard passed for certificate parameter.");
    }

    private static DnsPlanInput? TryBuildPlanInputFromEnvironment(string hostname)
    {
        var fqdn = Environment.GetEnvironmentVariable("NEOX_ACA_FQDN");
        var staticIp = Environment.GetEnvironmentVariable("NEOX_ACA_STATIC_IP");
        var asuid = Environment.GetEnvironmentVariable("NEOX_ACA_ASUID");

        if (string.IsNullOrWhiteSpace(fqdn) ||
            string.IsNullOrWhiteSpace(staticIp) ||
            string.IsNullOrWhiteSpace(asuid))
        {
            return null;
        }

        return new DnsPlanInput(hostname, fqdn, staticIp, asuid);
    }
}
