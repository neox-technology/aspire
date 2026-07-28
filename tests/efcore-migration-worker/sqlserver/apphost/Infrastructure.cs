using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.AppHost;

public static class Infrastructure
{
    public static IResourceBuilder<SqlServerServerResource> SqlServer { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        SqlServer = builder.AddSqlServer(ServiceNames.Infrastructure.SqlServer);
    }
}
