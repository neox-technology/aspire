using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public sealed class KeycloakJwtClientResource(
    string name,
    KeycloakRealmResource parent,
    string clientId,
    ParameterResource clientSecret,
    string audience)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>, IKeycloakRedirectClient {
    public string Audience { get; } = audience;
    public string ClientId { get; } = clientId;
    public ParameterResource ClientSecret { get; } = clientSecret;
    public KeycloakResource Keycloak => Parent.Parent;
    public KeycloakRealmResource Parent { get; } = parent;

    bool IKeycloakRedirectClient.UseDevWebOriginWildcard {
        get => UseDevWebOriginWildcard;
        set => UseDevWebOriginWildcard = value;
    }

    IList<Uri> IKeycloakRedirectClient.RedirectUrls => RedirectUrls;

    /// <summary>
    /// Keycloak <c>redirectUris</c> accumulated via <c>WithRedirectUrl</c> and <c>WithLocalRedirectUri</c>.
    /// </summary>
    internal List<Uri> RedirectUrls { get; } = [];

    /// <summary>
    /// When true, realm JSON includes <c>webOrigins: *</c> for Aspire ephemeral ports in Development.
    /// </summary>
    internal bool UseDevWebOriginWildcard { get; set; }
}