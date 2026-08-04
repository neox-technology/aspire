using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// One-shot background service that applies EF Core migrations for all registered
/// <see cref="DbContext"/> types (in registration order), then stops the host once.
/// </summary>
public sealed class EfCoreMigrationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<EfCoreMigrationWorker> _logger;

    public EfCoreMigrationWorker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<EfCoreMigrationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var registry = _serviceProvider.GetRequiredService<EfCoreMigrationRegistry>();

        foreach (var dbContextType in registry.DbContextTypes)
        {
            _logger.LogInformation("Applying database migrations for {DbContext}.", dbContextType.Name);

            using var scope = _serviceProvider.CreateScope();
            var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);

            await dbContext.Database.MigrateAsync(stoppingToken).ConfigureAwait(false);

            _logger.LogInformation("Database migrations applied for {DbContext}.", dbContextType.Name);
        }

        _hostApplicationLifetime.StopApplication();
    }
}
