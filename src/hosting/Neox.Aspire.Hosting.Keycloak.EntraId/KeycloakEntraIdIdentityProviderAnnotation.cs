using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Azure;

namespace Neox.Aspire.Hosting.Keycloak.EntraId;

internal sealed class KeycloakEntraIdIdentityProviderAnnotation : IResourceAnnotation {
    public required AzureEntraIdAppRegistrationResource AppRegistration { get; init; }
    public required ParameterResource ClientSecret { get; init; }
    public required EntraIdInstance Instance { get; init; }
}
