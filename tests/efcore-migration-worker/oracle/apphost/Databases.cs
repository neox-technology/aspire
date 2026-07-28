using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.AppHost;

public static class Databases
{
    public static IResourceBuilder<OracleDatabaseResource> Application { get; private set; } = null!;
    public static IResourceBuilder<OracleDatabaseResource> Application2 { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        // Service name must match the Free container PDB (not the Aspire resource name).
        Application = Infrastructure.Oracle.AddDatabase(ServiceNames.Databases.Application, "FREEPDB1");
        Application2 = Infrastructure.Oracle2.AddDatabase(ServiceNames.Databases.Application2, "FREEPDB1");
    }
}
