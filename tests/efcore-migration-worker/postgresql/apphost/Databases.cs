using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.AppHost;

public static class Databases
{
    public static IResourceBuilder<PostgresDatabaseResource> Application { get; private set; } = null!;
    public static IResourceBuilder<PostgresDatabaseResource> Application2 { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Application = Infrastructure.Postgres.AddDatabase(ServiceNames.Databases.Application, "application");
        Application2 = Infrastructure.Postgres.AddDatabase(ServiceNames.Databases.Application2, "application2");
    }
}
