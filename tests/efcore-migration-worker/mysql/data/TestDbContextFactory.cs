using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.MySql.Data;

public sealed class TestDbContextFactory : IDesignTimeDbContextFactory<TestDbContext>
{
    public TestDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseMySQL("Server=localhost;Database=neox_efmw_mysql_design;User=root;Password=root;")
            .Options;
        return new TestDbContext(options);
    }
}
