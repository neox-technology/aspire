using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire child resource for a Graph SPA platform on an
/// <see cref="AzureEntraIdAppRegistrationResource"/>. Not a provisioning resource;
/// Graph <c>spa.redirectUris</c> is emitted by the parent module.
/// </summary>
public sealed class AzureEntraIdSpaApplicationResource : Resource, IResourceWithParent<AzureEntraIdAppRegistrationResource>
{
    /// <summary>
    /// Initializes a new <see cref="AzureEntraIdSpaApplicationResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="parent">App registration that owns this SPA platform.</param>
    public AzureEntraIdSpaApplicationResource(string name, AzureEntraIdAppRegistrationResource parent)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Parent = parent;
    }

    /// <inheritdoc />
    public AzureEntraIdAppRegistrationResource Parent { get; }

    /// <summary>
    /// Graph <c>spa.redirectUris</c> accumulated via <c>WithRedirectUri</c>.
    /// </summary>
    internal List<Uri> RedirectUris { get; } = [];
}
