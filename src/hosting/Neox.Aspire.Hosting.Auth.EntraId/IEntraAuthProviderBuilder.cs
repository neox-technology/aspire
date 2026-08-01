using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra AuthOps provider builder — register apps with <see cref="IEntraAuthProviderBuilder.AddApp"/>.
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
    IResourceBuilder<AuthAppResource> AddApp(string name, Action<AuthAppOptions>? configure = null);
}
