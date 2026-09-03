using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public sealed class KeycloakRealmResource(
    string name,
    KeycloakResource parent,
    string realm,
    string importDirectory)
    : Resource(name), IResourceWithParent<KeycloakResource>
{
    public KeycloakResource Parent { get; } = parent;
    public string Realm { get; } = realm;
    public string ImportDirectory { get; } = importDirectory;
}
