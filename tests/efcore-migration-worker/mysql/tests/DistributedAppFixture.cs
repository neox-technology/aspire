using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql;

public sealed class DistributedAppFixture : IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Neox_Aspire_EntityFrameworkCore_MigrationWorker_Tests_MySql_AppHost>();

        App = await appHost.BuildAsync();
        await App.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class AppCollection : ICollectionFixture<DistributedAppFixture>
{
    public const string Name = "MySqlApp";
}
