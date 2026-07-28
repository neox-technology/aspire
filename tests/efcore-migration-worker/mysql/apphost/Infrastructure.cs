using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.AppHost;

public static class Infrastructure
{
    public static IResourceBuilder<MySqlServerResource> MySql { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        MySql = builder.AddMySql(ServiceNames.Infrastructure.MySql);
    }
}
