using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.EntityFrameworkCore;

/// <summary>
/// One-shot background service that applies EF Core migrations for <typeparamref name="TDbContext"/>
/// and then stops the host.
/// </summary>
/// <typeparam name="TDbContext">The <see cref="DbContext"/> type to migrate.</typeparam>
public sealed class EfCoreMigrationWorker<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly ILogger<EfCoreMigrationWorker<TDbContext>> _logger;

    public EfCoreMigrationWorker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<EfCoreMigrationWorker<TDbContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Applying database migrations for {DbContext}.", typeof(TDbContext).Name);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        await dbContext.Database.MigrateAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation("Database migrations applied for {DbContext}.", typeof(TDbContext).Name);
        _hostApplicationLifetime.StopApplication();
    }
}
