using Neox.Aspire.Hosting.Azure.Dns;

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

/// <summary>
/// A managed certificate on a Container Apps environment.
/// </summary>
public sealed record AzureManagedCertificateInfo(
    string Name,
    string? SubjectName,
    string Id);

/// <summary>
/// Planned custom-domain binding for a compute resource (no ARM side effects).
/// </summary>
public sealed record DomainBindingPlan(
    string TargetResourceName,
    string Hostname,
    string CertificateName,
    string ValidationMethod,
    HostnameKind Kind);

/// <summary>
/// Reads ACA ingress / environment properties required for DNS and certificate binding.
/// </summary>
public interface IAzureContainerAppReader
{
    Task<AzureContainerAppTargets> GetTargetsAsync(
        string containerAppName,
        string? resourceGroup,
        string? environmentName,
        CancellationToken cancellationToken);
}

/// <summary>
/// Azure Resource Manager operations for Container App custom-domain provisioning.
/// </summary>
public interface IAzureContainerAppClient : IAzureContainerAppReader
{
    /// <summary>
    /// Lists managed certificates on the Container Apps environment.
    /// </summary>
    Task<IReadOnlyList<AzureManagedCertificateInfo>> ListManagedCertificatesAsync(
        AzureContainerAppTargets targets,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates a managed certificate for <paramref name="hostname"/> (long-running).
    /// </summary>
    Task<AzureManagedCertificateInfo> CreateManagedCertificateAsync(
        AzureContainerAppTargets targets,
        string hostname,
        string certificateName,
        string validationMethod,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ensures <paramref name="hostname"/> is registered on the Container App without a certificate
    /// (<c>BindingType.Disabled</c>). No-op if the hostname is already present (does not detach an existing cert).
    /// </summary>
    /// <returns><see langword="true"/> when the hostname was added; <see langword="false"/> when already present.</returns>
    Task<bool> EnsureHostnameAsync(
        AzureContainerAppTargets targets,
        string hostname,
        CancellationToken cancellationToken);

    /// <summary>
    /// Binds an existing managed certificate to a custom hostname on the Container App.
    /// </summary>
    Task BindHostnameAsync(
        AzureContainerAppTargets targets,
        string hostname,
        string certificateId,
        CancellationToken cancellationToken);
}
