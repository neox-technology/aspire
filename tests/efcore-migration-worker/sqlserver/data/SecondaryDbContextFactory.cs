using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.Data;

public sealed class SecondaryDbContextFactory : IDesignTimeDbContextFactory<SecondaryDbContext>
{
    public SecondaryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SecondaryDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NeoxEfmwSqlSecondaryDesign;Trusted_Connection=True;")
            .Options;
        return new SecondaryDbContext(options);
    }
}
