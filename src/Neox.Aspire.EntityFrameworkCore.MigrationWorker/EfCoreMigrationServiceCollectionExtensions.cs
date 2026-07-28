using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// DI extensions for registering the EF Core migration background service.
/// </summary>
public static class EfCoreMigrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers a one-shot hosted service that applies EF Core migrations for
    /// <typeparamref name="TDbContext"/> and then stops the application host.
    /// The <typeparamref name="TDbContext"/> must already be registered in the service collection.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="DbContext"/> type to migrate.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddEfCoreMigrationService<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHostedService<EfCoreMigrationWorker<TDbContext>>();
        return services;
    }
}
