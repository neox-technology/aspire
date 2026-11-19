using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// One-shot background service that migrates every context registered through
/// <see cref="IEfCoreMigrationServicesBuilder"/>, then stops the host once.
/// </summary>
public sealed class EfCoreMigrationServicesWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<EfCoreMigrationServicesWorker> _logger;

    public EfCoreMigrationServicesWorker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<EfCoreMigrationServicesWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = _serviceProvider.GetRequiredService<EfCoreMigrationServicesBuilder>();

        foreach (var dbContextType in builder.DbContextTypes)
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
