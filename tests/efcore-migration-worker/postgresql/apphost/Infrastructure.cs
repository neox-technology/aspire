using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.AppHost;

public static class Infrastructure
{
    public static IResourceBuilder<PostgresServerResource> Postgres { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Postgres = builder.AddPostgres(ServiceNames.Infrastructure.Postgres);
    }
}
