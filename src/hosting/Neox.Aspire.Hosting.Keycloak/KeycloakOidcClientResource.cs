using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public sealed class KeycloakOidcClientResource(
    string name,
    KeycloakRealmResource parent,
    string clientId)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>, IKeycloakRedirectClient
{
    public KeycloakRealmResource Parent { get; } = parent;
    public string ClientId { get; } = clientId;
    public KeycloakResource Keycloak => Parent.Parent;

    /// <inheritdoc />
    internal List<Uri> RedirectUrls { get; } = [];

    /// <inheritdoc />
    internal bool UseDevWebOriginWildcard { get; set; }

    IList<Uri> IKeycloakRedirectClient.RedirectUrls => RedirectUrls;

    bool IKeycloakRedirectClient.UseDevWebOriginWildcard
    {
        get => UseDevWebOriginWildcard;
        set => UseDevWebOriginWildcard = value;
    }
}
