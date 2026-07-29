namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Azure Resource Manager operations for Container App custom-domain provisioning.
/// </summary>
public interface IAzureContainerAppClient : IAzureContainerAppReader
{
    /// <summary>
    /// Ensures the hostname is registered on the Container App and bound to a managed certificate.
    /// </summary>
    /// <param name="targets">Resolved Container App / environment targets.</param>
    /// <param name="hostname">Custom hostname to bind.</param>
    /// <param name="certificateName">Managed certificate resource name.</param>
    /// <param name="validationMethod"><c>HTTP</c> (apex) or <c>CNAME</c> (subdomain).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BindManagedHostnameAsync(
        AzureContainerAppTargets targets,
        string hostname,
        string certificateName,
        string validationMethod,
        CancellationToken cancellationToken);
}
