using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.EntityFrameworkCore;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.Data;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.ServiceDefaults;
using Xunit;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer;

[Collection(AppCollection.Name)]
public sealed class MigrationWorkerTests
{
    private readonly DistributedApplication _app;

    public MigrationWorkerTests(DistributedAppFixture fixture)
    {
        _app = fixture.App;
    }

    [Fact]
    public async Task SingleDatabase_AppliesMigrations()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await _app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.Migration,
            KnownResourceStates.Finished,
            cts.Token);

        await AssertMigrationsAppliedAsync<TestDbContext>(ServiceNames.Databases.Application, cts.Token);
    }

    [Fact]
    public async Task MultipleDatabases_ApplyMigrations()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await _app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.Migration,
            KnownResourceStates.Finished,
            cts.Token);
        await _app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.Migration2,
            KnownResourceStates.Finished,
            cts.Token);

        await AssertMigrationsAppliedAsync<TestDbContext>(ServiceNames.Databases.Application, cts.Token);
        await AssertMigrationsAppliedAsync<TestDbContext>(ServiceNames.Databases.Application2, cts.Token);
    }

    [Fact]
    public async Task SameProcess_MultipleDbContexts_ApplyMigrations()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await _app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.MigrationMulti,
            KnownResourceStates.Finished,
            cts.Token);

        await AssertMigrationsAppliedAsync<TestDbContext>(ServiceNames.Databases.ApplicationMulti, cts.Token);
        await AssertMigrationsAppliedAsync<SecondaryDbContext>(ServiceNames.Databases.ApplicationMulti2, cts.Token);
    }

    private async Task AssertMigrationsAppliedAsync<TContext>(string databaseResourceName, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var connectionString = await _app.GetConnectionStringAsync(databaseResourceName, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(connectionString), $"Missing connection string for '{databaseResourceName}'.");

        var optionsBuilder = new DbContextOptionsBuilder<TContext>().UseSqlServer(connectionString);
        await using var db = (TContext)Activator.CreateInstance(typeof(TContext), optionsBuilder.Options)!;
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        Assert.True(applied.Any(), $"Expected applied migrations for '{databaseResourceName}'.");
        Assert.False(pending.Any(), $"Expected no pending migrations for '{databaseResourceName}'.");
    }
}
