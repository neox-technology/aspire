using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Auth {
    public static IResourceBuilder<KeycloakResource> Keycloak { get; private set; } = null!;
    public static IResourceBuilder<KeycloakRealmResource> Realm { get; private set; } = null!;
    public static IResourceBuilder<KeycloakJwtClientResource> ApiClient { get; private set; } = null!;
    public static IResourceBuilder<KeycloakOidcClientResource> SpaClient { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder) {
        var username = builder.AddParameter("keycloak-admin-username", "admin");
        var password = builder.AddParameter("keycloak-admin-password", "admin", secret: true);
        var apiClientSecret = builder.AddParameter("keycloak-api-client-secret", "secret", secret: true);

        Keycloak = builder.AddKeycloak(
                ServiceNames.Auth.Keycloak,
                adminUsername: username,
                adminPassword: password);

        Realm = Keycloak.AddRealm();
        Realm.WithOrganization("neox", "neox-technology.com");

        ApiClient = Realm.AddJwtClient(
            ServiceNames.Auth.ApiClient,
            ServiceNames.Auth.ApiClientId,
            apiClientSecret)
            .WithLocalRedirectUri("/swagger/oauth2-redirect.html");

        SpaClient = Realm.AddOidcClient(
            ServiceNames.Auth.SpaClient,
            ServiceNames.Auth.SpaClientId)
            .WithLocalRedirectUri("/")
            .WithApiAudience(ServiceNames.Auth.ApiClientId);
    }
}
