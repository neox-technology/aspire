using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.Data;

public sealed class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NeoxEfmwSqlDesign;Trusted_Connection=True;")
            .Options;
        return new TestDbContext(options);
    }
}
