using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Optional Cloudflare auth bindings for <see cref="IDomainOpsProviderBuilder.Cloudflare"/>.
/// </summary>
public sealed class CloudflareDomainOpsProviderOptions
{
    /// <summary>
    /// API token. When null, a parameter named <c>{resource}-token</c> is created.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Token { get; set; }

    /// <summary>
    /// Optional Cloudflare account id filter.
    /// </summary>
    public IResourceBuilder<ParameterResource>? AccountId { get; set; }
}
