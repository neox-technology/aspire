using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public sealed class KeycloakJwtClientResource(
    string name,
    KeycloakRealmResource parent,
    string clientId,
    ParameterResource clientSecret,
    string audience)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>
{
    public KeycloakRealmResource Parent { get; } = parent;
    public string ClientId { get; } = clientId;
    public ParameterResource ClientSecret { get; } = clientSecret;
    public string Audience { get; } = audience;
    public KeycloakResource Keycloak => Parent.Parent;
}
