using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.EntityFrameworkCore;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.Data;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.ServiceDefaults;
using Xunit;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql;

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

        await AssertMigrationsAppliedAsync(ServiceNames.Databases.Application, cts.Token);
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

        await AssertMigrationsAppliedAsync(ServiceNames.Databases.Application, cts.Token);
        await AssertMigrationsAppliedAsync(ServiceNames.Databases.Application2, cts.Token);
    }

    private async Task AssertMigrationsAppliedAsync(string databaseResourceName, CancellationToken cancellationToken)
    {
        var connectionString = await _app.GetConnectionStringAsync(databaseResourceName, cancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(connectionString), $"Missing connection string for '{databaseResourceName}'.");

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new TestDbContext(options);
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        Assert.True(applied.Any(), $"Expected applied migrations for '{databaseResourceName}'.");
        Assert.False(pending.Any(), $"Expected no pending migrations for '{databaseResourceName}'.");
    }
}
