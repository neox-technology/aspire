using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;

namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Coordinates DomainOps plan / provision pipeline actions.
/// </summary>
public sealed class DomainOpsOrchestrator
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;
    private readonly DnsRecordPlanner _planner;
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
        OctoDnsZoneWriter zoneWriter,
        OctoDnsConfigWriter? configWriter = null,
        IAzureContainerAppClient? azureClient = null,
        DomainProvisioner? provisioner = null,
        IServiceProvider? services = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _zoneWriter = zoneWriter ?? throw new ArgumentNullException(nameof(zoneWriter));
        _configWriter = configWriter ?? new OctoDnsConfigWriter();
        _azureClient = azureClient;
        _provisioner = provisioner;
        _services = services;
    }

    public DomainOpsPlanProviderOutcome PlanProvider(
        DomainOpsProviderResource provider,
        IReadOnlyList<string> zoneNames,
        AzureCustomDomainOpsOptions options)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(zoneNames);
        ArgumentNullException.ThrowIfNull(options);

        var provisioner = GetProvisioner();
        var path = provisioner.PlanProviderConfig(provider, zoneNames, options);
        _logger.LogInformation(
            "plan-domain-{Provider}: wrote OctoDNS config {Path} for zones [{Zones}].",
            provider.ProviderSlug,
            path,
            string.Join(", ", zoneNames));

        return new DomainOpsPlanProviderOutcome(provider.ProviderSlug, path, zoneNames);
    }

    public async Task<DomainOpsPlanZoneOutcome> PlanZoneAsync(
        string zoneName,
        IReadOnlyList<ZoneBindingInput> bindings,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneName);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var provisioner = GetProvisioner();
        await provisioner.PlanZoneAsync(zoneName, bindings, provider, options, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "plan-domain-{Zone}: upserted {Count} hostname binding(s).",
            zoneName,
            bindings.Count);

        return new DomainOpsPlanZoneOutcome(zoneName, bindings.Count);
    }

    public async Task ProvisionZoneAsync(
        string zoneName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options,
        DnsPlan? waitPlan,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneName);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        var provisioner = GetProvisioner();
        await provisioner.ProvisionZoneAsync(zoneName, provider, options, waitPlan, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("provision-domain-{Zone}: OctoDNS sync applied.", zoneName);
    }

    public async Task<DomainOpsPlanCertificatesOutcome> PlanEnvCertificatesAsync(
        string environmentName,
        string containerAppResourceName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerAppResourceName);
        ArgumentNullException.ThrowIfNull(options);

        var provisioner = GetProvisioner();
        var azure = GetAzureClient();
        var targets = await azure.GetTargetsAsync(
                containerAppResourceName,
                resourceGroup: Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
                environmentName: null,
                cancellationToken)
            .ConfigureAwait(false);

        var certs = await provisioner.PlanEnvCertificatesAsync(targets, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "plan-{Env}-certificates: inventoried {Count} managed certificate(s).",
            environmentName,
            certs.Count);

        return new DomainOpsPlanCertificatesOutcome(environmentName, targets, certs);
    }

    public DomainBindingPlan PlanResourceDomain(
        IResource targetResource,
        string hostname,
        AzureCustomDomainOpsOptions options,
        string? certificateNameParameter)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(options);

        var provisioner = GetProvisioner();
        var plan = provisioner.PlanResourceDomain(
            targetResource.Name,
            hostname,
            options,
            certificateNameParameter);

        _logger.LogInformation(
            "plan-{Resource}-domain: hostname={Hostname}, cert={Certificate}, validation={Validation}.",
            targetResource.Name,
            plan.Hostname,
            plan.CertificateName,
            plan.ValidationMethod);

        return plan;
    }

    public async Task ProvisionEnvCertificatesAsync(
        AzureContainerAppTargets targets,
        IReadOnlyList<DomainBindingPlan> bindingPlans,
        IReadOnlyList<AzureManagedCertificateInfo> existingCertificates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(bindingPlans);
        ArgumentNullException.ThrowIfNull(existingCertificates);

        var provisioner = GetProvisioner();
        await provisioner.ProvisionEnvCertificatesAsync(
                targets,
                bindingPlans,
                existingCertificates,
                cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "provision-{Env}-certificates: ensured {Count} domain certificate(s).",
            targets.EnvironmentName,
            bindingPlans.Count);
    }

    public async Task<bool> ProvisionResourceDomainAsync(
        IResource targetResource,
        DomainBindingPlan plan,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);

        var azure = GetAzureClient();
        var provisioner = GetProvisioner();
        var appName = options.ContainerAppResourceName ?? targetResource.Name;

        var targets = await azure.GetTargetsAsync(
                appName,
                resourceGroup: Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
                environmentName: options.ContainerAppEnvironmentName,
                cancellationToken)
            .ConfigureAwait(false);

        var added = await provisioner.EnsureResourceHostnameAsync(targets, plan, cancellationToken)
            .ConfigureAwait(false);

        if (added)
        {
            _logger.LogInformation(
                "provision-{Resource}-domain: added hostname {Hostname} without certificate.",
                targetResource.Name,
                plan.Hostname);
        }
        else
        {
            _logger.LogInformation(
                "provision-{Resource}-domain: hostname {Hostname} already present (left unchanged).",
                targetResource.Name,
                plan.Hostname);
        }

        return added;
    }

    public async Task BindResourceDomainAsync(
        IResource targetResource,
        DomainBindingPlan plan,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);

        var azure = GetAzureClient();
        var provisioner = GetProvisioner();
        var appName = options.ContainerAppResourceName ?? targetResource.Name;

        var targets = await azure.GetTargetsAsync(
                appName,
                resourceGroup: Environment.GetEnvironmentVariable("Azure__ResourceGroup"),
                environmentName: options.ContainerAppEnvironmentName,
                cancellationToken)
            .ConfigureAwait(false);

        var certs = await azure.ListManagedCertificatesAsync(targets, cancellationToken).ConfigureAwait(false);
        await provisioner.BindResourceDomainAsync(targets, plan, certs, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "deploy-{Resource}-domain: bound {Hostname} to certificate '{Certificate}'.",
            targetResource.Name,
            plan.Hostname,
            plan.CertificateName);
    }

    /// <summary>
    /// Plans records and writes OctoDNS zone YAML (used by tests).
    /// </summary>
    public string WritePlannedZone(DnsPlanInput input, string zoneDirectory)
    {
        var plan = _planner.Plan(input);
        return _zoneWriter.WriteToDirectory(plan, zoneDirectory);
    }

    private DomainProvisioner GetProvisioner()
    {
        if (_provisioner is not null)
        {
            return _provisioner;
        }

        var azure = GetAzureClient();
        return new DomainProvisioner(
            _processRunner,
            azure,
            _planner,
            zoneUpserter: null,
            _configWriter);
    }

    private IAzureContainerAppClient GetAzureClient()
        => _azureClient
           ?? (_services is not null
               ? ArmAzureContainerAppClient.Create(_services)
               : throw new InvalidOperationException(
                   "IAzureContainerAppClient is required. Pass an ARM client or IServiceProvider with ITokenCredentialProvider."));
}
