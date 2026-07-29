using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Fluent builder that selects an OctoDNS DNS provider after <see cref="DomainOpsProviderExtensions.AddDomainOpsProvider"/>.
/// </summary>
public interface IDomainOpsProviderBuilder
{
    /// <summary>
    /// Configures Cloudflare as the OctoDNS target provider.
    /// </summary>
    IResourceBuilder<CloudflareDomainOpsProviderResource> Cloudflare(CloudflareDomainOpsProviderOptions? options = null);

    /// <summary>
    /// Configures OVH as the OctoDNS target provider.
    /// </summary>
    IResourceBuilder<OvhDomainOpsProviderResource> Ovh(OvhDomainOpsProviderOptions? options = null);
}
