using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Neox.Aspire.EntityFrameworkCore;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.Data;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.MigrationService;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var connectionName = builder.Configuration["ConnectionName"]
            ?? ServiceNames.Databases.Application;

        builder.AddNpgsqlDbContext<TestDbContext>(connectionName);
        builder.Services.AddEfCoreMigrationService<TestDbContext>();

        builder.Build().Run();
    }
}
