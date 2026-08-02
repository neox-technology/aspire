using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra AuthOps provider builder — register apps with <see cref="AddAppRegistration"/>.
/// </summary>
public interface IEntraAuthProviderBuilder
{
    /// <summary>
    /// The Entra provider resource.
    /// </summary>
    IResourceBuilder<EntraAuthOpsResource> Resource { get; }

    /// <summary>
    /// Adds an app registration under this Entra provider.
    /// </summary>
    /// <param name="name">Aspire resource name (slug for pipeline steps and parameters).</param>
    /// <param name="displayName">Required display name in Entra ID.</param>
    IResourceBuilder<EntraAuthAppRegistrationResource> AddAppRegistration(string name, string displayName);
}
