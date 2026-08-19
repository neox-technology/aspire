using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Auth {
    public static IResourceBuilder<KeycloakResource> Keycloak { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder) {
        var username = builder.AddParameter("keycloak-admin-username", "admin");
        var password = builder.AddParameter("keycloak-admin-password", "admin", secret: true);

        Keycloak = builder.AddKeycloak(
                ServiceNames.Auth.Keycloak,
                adminUsername: username,
                adminPassword: password);

        var realm = Keycloak.AddRealm();
        var org = realm.WithOrganization("neox", "neox-technology.com");
    }
}