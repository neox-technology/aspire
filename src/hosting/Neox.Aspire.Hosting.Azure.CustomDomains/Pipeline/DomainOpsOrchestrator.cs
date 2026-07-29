using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;

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
    private readonly OctoDnsConfigWriter _configWriter;
    private readonly IAzureContainerAppClient? _azureClient;
    private readonly DomainProvisioner? _provisioner;
    private readonly IServiceProvider? _services;

    public DomainOpsOrchestrator(IProcessRunner processRunner, ILogger logger, IServiceProvider? services = null)
        : this(
            processRunner,
            logger,
            new DnsRecordPlanner(),
            new DnsRecordVerifier(),
            new OctoDnsZoneWriter(),
            new OctoDnsConfigWriter(),
            azureClient: null,
            provisioner: null,
            services: services)
    {
    }

    public DomainOpsOrchestrator(
        IProcessRunner processRunner,
        ILogger logger,
        DnsRecordPlanner planner,
        DnsRecordVerifier verifier,
        OctoDnsZoneWriter zoneWriter,
        OctoDnsConfigWriter? configWriter = null,
        IAzureContainerAppClient? azureClient = null,
        DomainProvisioner? provisioner = null,
        IServiceProvider? services = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _zoneWriter = zoneWriter ?? throw new ArgumentNullException(nameof(zoneWriter));
        _configWriter = configWriter ?? new OctoDnsConfigWriter();
        _azureClient = azureClient;
        _provisioner = provisioner;
        _services = services;
    }

    public async Task<DomainOpsVerifyOutcome> VerifyAsync(
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
            return new DomainOpsVerifyOutcome(
                targetResource.Name,
                hostname,
                DomainOpsDnsCheckStatus.SkippedNoPlanInput);
        }

        var plan = _planner.Plan(planInput);
        _logger.LogInformation(
            "domain-verify: planned {Count} DNS records for {Hostname} ({Kind}).",
            plan.Records.Count,
            hostname,
            plan.Kind);

        if (observedRecords is null)
        {
            return new DomainOpsVerifyOutcome(
                targetResource.Name,
                hostname,
                DomainOpsDnsCheckStatus.PlannedOnly,
                plan.Kind,
                plan.Records.Count);
        }

        var drift = _verifier.FindDrift(plan.Records, observedRecords);
        if (drift.Count > 0)
        {
            throw new InvalidOperationException(
                "DNS drift detected:" + Environment.NewLine + string.Join(Environment.NewLine, drift));
        }

        _logger.LogInformation("domain-verify: DNS records match expected plan.");
        return new DomainOpsVerifyOutcome(
            targetResource.Name,
            hostname,
            DomainOpsDnsCheckStatus.Matched,
            plan.Kind,
            plan.Records.Count);
    }

    public Task<DomainOpsGuardOutcome> GuardAsync(
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
        => GuardCoreAsync(certificateName, options, cancellationToken);

    public async Task<DomainOpsProvisionOutcome> ProvisionAsync(
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var hostname = await customDomain.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new InvalidOperationException("Custom domain parameter is empty. Set Parameters__customDomain.");
        }

        options.ContainerAppResourceName ??= targetResource.Name;

        var azureClient = _azureClient
            ?? (_services is not null
                ? ArmAzureContainerAppClient.Create(_services)
                : throw new InvalidOperationException(
                    "IAzureContainerAppClient is required. Pass an ARM client or IServiceProvider with ITokenCredentialProvider."));

        var provisioner = _provisioner
            ?? new DomainProvisioner(
                _processRunner,
                azureClient,
                _planner,
                _zoneWriter,
                _configWriter);

        var certName = await provisioner.ProvisionAsync(hostname, provider, options, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "domain-provision completed for {Hostname}; certificate '{Certificate}' (GitHub variable {Variable}).",
            hostname,
            certName,
            options.CertificateGitHubVariableName);

        return new DomainOpsProvisionOutcome(
            targetResource.Name,
            hostname,
            certName,
            options.CertificateGitHubVariableName);
    }

    /// <summary>
    /// Plans records and writes OctoDNS zone YAML (used by provision and tests).
    /// </summary>
    public string WritePlannedZone(DnsPlanInput input, string zoneDirectory)
    {
        var plan = _planner.Plan(input);
        return _zoneWriter.WriteToDirectory(plan, zoneDirectory);
    }

    private async Task<DomainOpsGuardOutcome> GuardCoreAsync(
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.RequireCertificateName)
        {
            _logger.LogInformation("domain-guard skipped because RequireCertificateName is false.");
            return new DomainOpsGuardOutcome(Skipped: true);
        }

        var value = await certificateName.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Certificate name parameter is empty. Set Parameters__certificateName (or disable RequireCertificateName for bootstrap).");
        }

        _logger.LogInformation("domain-guard passed for certificate parameter.");
        return new DomainOpsGuardOutcome(Skipped: false, CertificateName: value);
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
