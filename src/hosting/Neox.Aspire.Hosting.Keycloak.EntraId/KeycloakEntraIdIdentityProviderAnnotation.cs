using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Azure;

namespace Neox.Aspire.Hosting.Keycloak.EntraId;

internal sealed class KeycloakEntraIdIdentityProviderAnnotation : IResourceAnnotation {
    public AzureEntraIdAppRegistrationResource AppRegistration { get; init; }
    public EntraIdInstance Instance { get; init; }
}