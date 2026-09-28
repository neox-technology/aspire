using Aspire.Hosting.ApplicationModel;
using Neox.Keycloak.Provisioning.Realm;

namespace Neox.Aspire.Hosting.Keycloak;

/// <summary>
/// Aspire child resource for a Keycloak user that can be imported into a realm.
/// </summary>
public sealed class KeycloakUserResource(
    string name,
    KeycloakResource parent,
    string username,
    ParameterResource password)
    : Resource(name), IResourceWithParent<KeycloakResource>
{
    public KeycloakResource Parent { get; } = parent;

    public string Username { get; } = username;

    public ParameterResource Password { get; } = password;

    internal UserRepresentation Representation { get; } = new()
    {
        Username = username,
        Enabled = true,
        EmailVerified = true,
    };
}
