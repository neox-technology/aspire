using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Provisioning;

namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Outcome of planning an OctoDNS provider config file.
/// </summary>
public sealed record DomainOpsPlanProviderOutcome(
    string ProviderSlug,
    string ConfigPath,
    IReadOnlyList<string> ZoneNames);

/// <summary>
/// Outcome of planning a DNS zone YAML.
/// </summary>
public sealed record DomainOpsPlanZoneOutcome(
    string ZoneName,
    int BindingCount);

/// <summary>
/// Outcome of inventoring managed certificates on an ACA environment.
/// </summary>
public sealed record DomainOpsPlanCertificatesOutcome(
    string EnvironmentName,
    AzureContainerAppTargets Targets,
    IReadOnlyList<AzureManagedCertificateInfo> Certificates);
