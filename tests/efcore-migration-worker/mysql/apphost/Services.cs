using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.AppHost;

public static class Services
{
    public static IResourceBuilder<ProjectResource> Migration { get; private set; } = null!;
    public static IResourceBuilder<ProjectResource> Migration2 { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Migration = builder
            .AddProject<Projects.Neox_Aspire_EntityFrameworkCore_MigrationWorker_Tests_MySql_MigrationService>(
                ServiceNames.Workers.Migration)
            .WithReference(Databases.Application)
            .WaitFor(Databases.Application)
            .WithEnvironment("ConnectionName", ServiceNames.Databases.Application);

        Migration2 = builder
            .AddProject<Projects.Neox_Aspire_EntityFrameworkCore_MigrationWorker_Tests_MySql_MigrationService>(
                ServiceNames.Workers.Migration2)
            .WithReference(Databases.Application2)
            .WaitFor(Databases.Application2)
            .WithEnvironment("ConnectionName", ServiceNames.Databases.Application2);
    }
}
