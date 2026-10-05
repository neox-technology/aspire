using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public sealed class KeycloakOidcClientResource(
    string name,
    KeycloakRealmResource parent,
    string clientId)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>, IKeycloakRedirectClient {
    public string ClientId { get; } = clientId;
    public KeycloakResource Keycloak => Parent.Parent;
    public KeycloakRealmResource Parent { get; } = parent;

    bool IKeycloakRedirectClient.UseDevWebOriginWildcard {
        get => UseDevWebOriginWildcard;
        set => UseDevWebOriginWildcard = value;
    }

    IList<Uri> IKeycloakRedirectClient.RedirectUrls => RedirectUrls;

    /// <inheritdoc />
    internal List<Uri> RedirectUrls { get; } = [];

    /// <inheritdoc />
    internal bool UseDevWebOriginWildcard { get; set; }
}