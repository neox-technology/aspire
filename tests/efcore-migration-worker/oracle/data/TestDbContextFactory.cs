using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.Data;

public sealed class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseOracle("User Id=system;Password=oracle;Data Source=localhost:1521/FREEPDB1")
            .Options;
        return new TestDbContext(options);
    }
}
