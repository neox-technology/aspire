using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// DI extensions for registering the EF Core migration background service.
/// </summary>
public static class EfCoreMigrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> for one-shot EF Core migration in this process.
    /// Multiple calls (distinct types) share a single hosted <see cref="EfCoreMigrationWorker"/>;
    /// migrations run sequentially in registration order, then the host stops once.
    /// The <typeparamref name="TDbContext"/> must already be registered in the service collection.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="DbContext"/> type to migrate.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddEfCoreMigrationService<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var registry = GetOrAddRegistry(services);
        registry.Register(typeof(TDbContext));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EfCoreMigrationWorker>());
        return services;
    }

    private static EfCoreMigrationRegistry GetOrAddRegistry(IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(EfCoreMigrationRegistry));
        if (existing?.ImplementationInstance is EfCoreMigrationRegistry registry)
        {
            return registry;
        }

        registry = new EfCoreMigrationRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
