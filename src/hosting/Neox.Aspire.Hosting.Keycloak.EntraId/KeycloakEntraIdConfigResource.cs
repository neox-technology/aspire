using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;

namespace Neox.Aspire.Hosting.Keycloak.EntraId;

/// <summary>
/// Non-parented gate that writes Entra OIDC IdP config into realm JSON after Entra outputs are ready.
/// Keycloak <c>WaitFor</c>s this resource (not the parented IdP child).
/// </summary>
internal sealed class KeycloakEntraIdConfigResource : Resource, IResourceWithWaitSupport
{
    public KeycloakEntraIdConfigResource(string name, KeycloakIdentityProviderResource identityProvider)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(identityProvider);
        IdentityProvider = identityProvider;
    }

    public KeycloakIdentityProviderResource IdentityProvider { get; }
}
