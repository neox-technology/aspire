namespace Neox.Aspire.Hosting.Azure.Provisioning;

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
