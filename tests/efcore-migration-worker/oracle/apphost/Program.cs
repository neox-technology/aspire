using Aspire.Hosting;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.AppHost;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        Infrastructure.Configure(builder);
        Databases.Configure(builder);
        Services.Configure(builder);

        builder.Build().Run();
    }
}
