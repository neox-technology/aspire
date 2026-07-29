using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Optional OVH auth / endpoint bindings for <see cref="IDomainOpsProviderBuilder.Ovh"/>.
/// </summary>
public sealed class OvhDomainOpsProviderOptions
{
    /// <summary>
    /// OVH API endpoint (e.g. <c>ovh-eu</c>). Written as a literal in <c>octodns.yaml</c>.
    /// </summary>
    public string Endpoint { get; set; } = "ovh-eu";

    /// <summary>
    /// Application key. When null, a parameter named <c>{resource}-application-key</c> is created.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ApplicationKey { get; set; }

    /// <summary>
    /// Application secret. When null, a parameter named <c>{resource}-application-secret</c> is created.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ApplicationSecret { get; set; }

    /// <summary>
    /// Consumer key. When null, a parameter named <c>{resource}-consumer-key</c> is created.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ConsumerKey { get; set; }
}
