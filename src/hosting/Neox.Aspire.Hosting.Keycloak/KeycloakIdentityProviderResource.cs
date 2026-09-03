using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

/// <summary>
/// Aspire child resource for a Keycloak identity provider entry in realm import JSON.
/// </summary>
public sealed class KeycloakIdentityProviderResource(
    string name,
    KeycloakRealmResource parent,
    string alias,
    string providerId)
    : Resource(name), IResourceWithParent<KeycloakRealmResource>, IResourceWithWaitSupport
{
    public KeycloakRealmResource Parent { get; } = parent;

    public string Alias { get; } = alias;

    public string ProviderId { get; } = providerId;

    public KeycloakResource Keycloak => Parent.Parent;
}
