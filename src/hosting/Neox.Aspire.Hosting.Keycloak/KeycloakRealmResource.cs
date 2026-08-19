using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public sealed class KeycloakRealmResource(
    string name,
    string realm,
    string authServerUrl,
    string? clientId,
    string? clientSecret) : Resource(name)
{
    public string Realm { get; } = realm;
    public string AuthServerUrl { get; } = authServerUrl.TrimEnd('/');
    public string? ClientId { get; } = clientId;
    public string? ClientSecret { get; } = clientSecret;
}
