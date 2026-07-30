namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Reads ACA ingress / environment properties required for DNS and certificate binding.
/// </summary>
public interface IAzureContainerAppReader
{
    /// <summary>
    /// Resolves Container App FQDN, environment static IP, and custom-domain verification id.
    /// </summary>
    Task<AzureContainerAppTargets> GetTargetsAsync(
        string containerAppName,
        string? resourceGroup,
        string? environmentName,
        CancellationToken cancellationToken);
}
