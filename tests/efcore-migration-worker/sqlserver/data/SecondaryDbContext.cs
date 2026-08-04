using Microsoft.EntityFrameworkCore;

namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.Data;

public sealed class SecondarySampleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class SecondaryDbContext : DbContext
{
    public SecondaryDbContext(DbContextOptions<SecondaryDbContext> options)
        : base(options)
    {
    }

    public DbSet<SecondarySampleEntity> Samples => Set<SecondarySampleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecondarySampleEntity>(entity =>
        {
            entity.ToTable("Samples");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        });
    }
}
