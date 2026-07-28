using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.AppHost;

public static class Infrastructure
{
    public static IResourceBuilder<OracleDatabaseServerResource> Oracle { get; private set; } = null!;
    public static IResourceBuilder<OracleDatabaseServerResource> Oracle2 { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        // Oracle Free only ships FREEPDB1; AddDatabase does not create PDBs (dotnet/aspire#15024).
        Oracle = builder.AddOracle(ServiceNames.Infrastructure.Oracle);
        Oracle2 = builder.AddOracle(ServiceNames.Infrastructure.Oracle2);
    }
}
