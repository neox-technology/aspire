using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Neox.Aspire.EntityFrameworkCore;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.Data;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.MigrationService;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        var connectionName = builder.Configuration["ConnectionName"]
            ?? ServiceNames.Databases.Application;

        var connectionString = builder.Configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException($"Connection string '{connectionName}' was not found.");

        builder.Services.AddDbContext<TestDbContext>(options => options.UseMySQL(connectionString));
        builder.Services.AddEfCoreMigrationService<TestDbContext>();

        builder.Build().Run();
    }
}
