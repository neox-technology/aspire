using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire child resource for a Graph web platform on an
/// <see cref="AzureEntraIdAppRegistrationResource"/>. Not a provisioning resource;
/// Graph <c>web.redirectUris</c> is emitted by the parent module.
/// </summary>
public sealed class AzureEntraIdWebApplicationResource : Resource, IResourceWithParent<AzureEntraIdAppRegistrationResource>
{
    /// <summary>
    /// Initializes a new <see cref="AzureEntraIdWebApplicationResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="parent">App registration that owns this web platform.</param>
    public AzureEntraIdWebApplicationResource(string name, AzureEntraIdAppRegistrationResource parent)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Parent = parent;
    }

    /// <inheritdoc />
    public AzureEntraIdAppRegistrationResource Parent { get; }

    /// <summary>
    /// Graph <c>web.redirectUris</c> accumulated via <c>WithRedirectUri</c>.
    /// </summary>
    internal List<Uri> RedirectUris { get; } = [];
}
