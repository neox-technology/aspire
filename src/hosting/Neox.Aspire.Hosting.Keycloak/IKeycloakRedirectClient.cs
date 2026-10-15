namespace Neox.Aspire.Hosting.Keycloak;

internal interface IKeycloakRedirectClient {
    string ClientId { get; }
    KeycloakRealmResource Parent { get; }
    IList<Uri> RedirectUrls { get; }

    bool UseDevWebOriginWildcard { get; set; }
}