using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Infrastructure
{
    public static IResourceBuilder<PostgresServerResource> Postgres { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Postgres = builder.AddPostgres(ServiceNames.Infrastructure.Postgres)
            .WithDataVolume();
    }
}
