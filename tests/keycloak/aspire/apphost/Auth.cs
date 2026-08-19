using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;
using Neox.Keycloak.Provisioning.Realm;
using System.Collections.Concurrent;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Auth {
    public static IResourceBuilder<KeycloakResource> Keycloak { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder) {
        var username = builder.AddParameter("keycloak-admin-username", "admin");
        var password = builder.AddParameter("keycloak-admin-password", "admin", secret: true);

        var database = Infrastructure.Postgres.AddDatabase(
            ServiceNames.Databases.Keycloak,
            "keycloakdb");

        Keycloak = builder.AddKeycloak(
                ServiceNames.Auth.Keycloak,
                adminUsername: username,
                adminPassword: password)
            .WithLifetime(ContainerLifetime.Persistent)
            .WithPostgres(database);

        Keycloak.AddRealm(r => {
            r.Clients = new ConcurrentBag<ClientRepresentation>
            {
                new() { ClientId = "neox-spa", PublicClient = true, Enabled = true }
            };
        });
    }
}