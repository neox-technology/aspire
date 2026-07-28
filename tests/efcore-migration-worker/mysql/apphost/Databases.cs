using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.AppHost;

public static class Databases
{
    public static IResourceBuilder<MySqlDatabaseResource> Application { get; private set; } = null!;
    public static IResourceBuilder<MySqlDatabaseResource> Application2 { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Application = Infrastructure.MySql.AddDatabase(ServiceNames.Databases.Application, "application");
        Application2 = Infrastructure.MySql.AddDatabase(ServiceNames.Databases.Application2, "application2");
    }
}
