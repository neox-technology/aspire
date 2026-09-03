using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.AppHost;

public static class Databases
{
    public static IResourceBuilder<SqlServerDatabaseResource> Application { get; private set; } = null!;
    public static IResourceBuilder<SqlServerDatabaseResource> Application2 { get; private set; } = null!;
    public static IResourceBuilder<SqlServerDatabaseResource> ApplicationMulti { get; private set; } = null!;
    public static IResourceBuilder<SqlServerDatabaseResource> ApplicationMulti2 { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Application = Infrastructure.SqlServer.AddDatabase(ServiceNames.Databases.Application, "application");
        Application2 = Infrastructure.SqlServer.AddDatabase(ServiceNames.Databases.Application2, "application2");
        ApplicationMulti = Infrastructure.SqlServer.AddDatabase(ServiceNames.Databases.ApplicationMulti, "application_multi");
        ApplicationMulti2 = Infrastructure.SqlServer.AddDatabase(ServiceNames.Databases.ApplicationMulti2, "application_multi_2");
    }
}
