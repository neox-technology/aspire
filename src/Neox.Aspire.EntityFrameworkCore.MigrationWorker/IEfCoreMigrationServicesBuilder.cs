using Microsoft.EntityFrameworkCore;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// Collects the <see cref="DbContext"/> types migrated by <see cref="EfCoreMigrationServicesWorker"/>.
/// </summary>
public interface IEfCoreMigrationServicesBuilder
{
    /// <summary>
    /// Adds <typeparamref name="TDbContext"/> to the migration list. A repeated type is ignored.
    /// </summary>
    /// <typeparam name="TDbContext">The context to migrate. It must already be registered in DI.</typeparam>
    /// <returns>The same builder, for chaining.</returns>
    IEfCoreMigrationServicesBuilder AddDbContextToMigrate<TDbContext>()
        where TDbContext : DbContext;
}
