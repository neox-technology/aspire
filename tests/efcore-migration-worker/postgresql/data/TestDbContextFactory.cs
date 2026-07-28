using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.PostgreSql.Data;

public sealed class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql("Host=localhost;Database=neox_efmw_pg_design;Username=postgres;Password=postgres")
            .Options;
        return new TestDbContext(options);
    }
}
