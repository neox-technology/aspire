using Neox.Aspire.Hosting.Keycloak;
using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Auth
{
    public static IResourceBuilder<KeycloakRealmResource> Realm { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        var keycloakUrl = builder.Configuration["Keycloak:Url"] ?? "http://localhost:8080";
        var clientSecret = builder.Configuration["Keycloak:ClientSecret"] ?? "change-me";

        Realm = builder.AddKeycloakRealm(
            ServiceNames.Auth.Realm,
            "neox",
            keycloakUrl,
            ServiceNames.Auth.ClientId,
            clientSecret);
    }
}
