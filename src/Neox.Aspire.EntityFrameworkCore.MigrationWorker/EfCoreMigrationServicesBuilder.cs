using Microsoft.EntityFrameworkCore;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// Ordered list of <see cref="DbContext"/> types for <see cref="EfCoreMigrationServicesWorker"/>.
/// </summary>
public sealed class EfCoreMigrationServicesBuilder : IEfCoreMigrationServicesBuilder
{
    private readonly List<Type> _dbContextTypes = [];

    /// <summary>
    /// Gets the context types in registration order.
    /// </summary>
    public IReadOnlyList<Type> DbContextTypes => _dbContextTypes;

    /// <inheritdoc />
    public IEfCoreMigrationServicesBuilder AddDbContextToMigrate<TDbContext>()
        where TDbContext : DbContext
    {
        var dbContextType = typeof(TDbContext);

        if (!_dbContextTypes.Contains(dbContextType))
        {
            _dbContextTypes.Add(dbContextType);
        }

        return this;
    }
}
