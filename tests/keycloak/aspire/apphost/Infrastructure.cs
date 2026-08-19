using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Infrastructure {
    public static IResourceBuilder<PostgresServerResource> Postgres { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder) {
        var pgUsername = builder.AddParameter("postgres-username", "postgres");
        var pgPassword = builder.AddParameter("postgres-password", "postgres", secret: true);

        Postgres = builder.AddPostgres(ServiceNames.Infrastructure.Postgres, pgUsername, pgPassword)
            .WithDataVolume()
            .WithLifetime(ContainerLifetime.Persistent);
    }
}