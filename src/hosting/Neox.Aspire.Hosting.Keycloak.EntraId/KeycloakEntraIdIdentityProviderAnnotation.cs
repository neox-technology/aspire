using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Azure;

namespace Neox.Aspire.Hosting.Keycloak.EntraId;

internal sealed class KeycloakEntraIdIdentityProviderAnnotation : IResourceAnnotation
{
    public required EntraIdInstance Instance { get; init; }

    public required AzureEntraIdAppRegistrationResource AppRegistration { get; init; }
}
