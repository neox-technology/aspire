using Neox.Aspire.Hosting.Azure.Provisioning;

namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Outcome of inventoring managed certificates on an ACA environment.
/// </summary>
public sealed record DomainOpsPlanCertificatesOutcome(
    string EnvironmentName,
    AzureContainerAppTargets Targets,
    IReadOnlyList<AzureManagedCertificateInfo> Certificates);
