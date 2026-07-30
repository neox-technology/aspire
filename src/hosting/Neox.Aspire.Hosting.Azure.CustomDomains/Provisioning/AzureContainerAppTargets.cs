namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Targets discovered from a deployed Azure Container App.
/// </summary>
public sealed record AzureContainerAppTargets(
    string ContainerAppName,
    string ResourceGroup,
    string EnvironmentName,
    string Fqdn,
    string StaticIp,
    string CustomDomainVerificationId);
