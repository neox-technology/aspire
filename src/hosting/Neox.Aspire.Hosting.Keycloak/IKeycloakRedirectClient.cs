namespace Neox.Aspire.Hosting.Keycloak;

internal interface IKeycloakRedirectClient
{
    KeycloakRealmResource Parent { get; }

    string ClientId { get; }

    IList<Uri> RedirectUrls { get; }

    bool UseDevWebOriginWildcard { get; set; }
}
