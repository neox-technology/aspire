using Microsoft.EntityFrameworkCore;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// Ordered list of <see cref="DbContext"/> types to migrate in a single process.
/// </summary>
internal sealed class EfCoreMigrationRegistry
{
    private readonly List<Type> _dbContextTypes = [];

    public IReadOnlyList<Type> DbContextTypes => _dbContextTypes;

    public void Register(Type dbContextType)
    {
        ArgumentNullException.ThrowIfNull(dbContextType);

        if (!typeof(DbContext).IsAssignableFrom(dbContextType))
        {
            throw new ArgumentException(
                $"Type '{dbContextType}' must inherit from {nameof(DbContext)}.",
                nameof(dbContextType));
        }

        if (_dbContextTypes.Contains(dbContextType))
        {
            return;
        }

        _dbContextTypes.Add(dbContextType);
    }
}
