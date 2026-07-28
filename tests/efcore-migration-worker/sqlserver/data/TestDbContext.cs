using Microsoft.EntityFrameworkCore;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.Data;

public sealed class SampleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    public DbSet<SampleEntity> Samples => Set<SampleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SampleEntity>(entity =>
        {
            entity.ToTable("Samples");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        });
    }
}
