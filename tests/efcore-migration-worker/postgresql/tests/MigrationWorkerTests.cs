using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.Data;
using Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.ServiceDefaults;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql;

[TestClass]
public sealed class MigrationWorkerTests
{
    private static DistributedApplication? s_app;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Neox_Aspire_EntityFrameworkCore_MigrationWorker_Tests_PostgreSql_AppHost>();

        s_app = await appHost.BuildAsync();
        await s_app.StartAsync();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static async Task ClassCleanup()
    {
        if (s_app is not null)
        {
            await s_app.DisposeAsync();
            s_app = null;
        }
    }

    [TestMethod]
    [Timeout(180_000)]
    public async Task SingleDatabase_AppliesMigrations()
    {
        Assert.IsNotNull(s_app);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await s_app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.Migration,
            KnownResourceStates.Finished,
            cts.Token);

        await AssertMigrationsAppliedAsync(ServiceNames.Databases.Application, cts.Token);
    }

    [TestMethod]
    [Timeout(180_000)]
    public async Task MultipleDatabases_ApplyMigrations()
    {
        Assert.IsNotNull(s_app);
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await s_app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.Migration,
            KnownResourceStates.Finished,
            cts.Token);
        await s_app.ResourceNotifications.WaitForResourceAsync(
            ServiceNames.Workers.Migration2,
            KnownResourceStates.Finished,
            cts.Token);

        await AssertMigrationsAppliedAsync(ServiceNames.Databases.Application, cts.Token);
        await AssertMigrationsAppliedAsync(ServiceNames.Databases.Application2, cts.Token);
    }

    private static async Task AssertMigrationsAppliedAsync(string databaseResourceName, CancellationToken cancellationToken)
    {
        Assert.IsNotNull(s_app);
        var connectionString = await s_app.GetConnectionStringAsync(databaseResourceName, cancellationToken);
        Assert.IsFalse(string.IsNullOrWhiteSpace(connectionString), $"Missing connection string for '{databaseResourceName}'.");

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new TestDbContext(options);
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        Assert.IsTrue(applied.Any(), $"Expected applied migrations for '{databaseResourceName}'.");
        Assert.IsFalse(pending.Any(), $"Expected no pending migrations for '{databaseResourceName}'.");
    }
}
