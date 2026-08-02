using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Publishes Entra AuthOps dashboard statuses after resources are created,
/// when Auth parameters are set, and on a periodic refresh loop.
/// </summary>
internal sealed class EntraAuthDashboardLifecycleSubscriber : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventing);
        ArgumentNullException.ThrowIfNull(executionContext);

        if (!executionContext.IsRunMode)
        {
            return Task.CompletedTask;
        }

        eventing.Subscribe<AfterResourcesCreatedEvent>(async (@event, ct) =>
        {
            var services = @event.Services;
            var model = @event.Model;
            var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
            var notifications = services.GetRequiredService<ResourceNotificationService>();
            var logger = services.GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(EntraAuthDashboardLifecycleSubscriber));

            var authParameterNames = CollectAuthParameterNames(model);

            await statusService.RefreshAsync(services, model, ct).ConfigureAwait(false);

            _ = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(EntraAuthDashboardStatusService.RefreshInterval);
                try
                {
                    while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    {
                        try
                        {
                            await statusService.RefreshAsync(services, model, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger?.LogDebug(ex, "Entra AuthOps dashboard status refresh failed.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // AppHost shutting down.
                }
            }, CancellationToken.None);

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var resourceEvent in notifications.WatchAsync(ct).ConfigureAwait(false))
                    {
                        if (!authParameterNames.Contains(resourceEvent.Resource.Name))
                        {
                            continue;
                        }

                        try
                        {
                            await statusService.RefreshAsync(services, model, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger?.LogDebug(ex, "Entra AuthOps dashboard status refresh after parameter update failed.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // AppHost shutting down.
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Entra AuthOps parameter watch failed.");
                }
            }, CancellationToken.None);
        });

        return Task.CompletedTask;
    }

    private static HashSet<string> CollectAuthParameterNames(DistributedApplicationModel model)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var provider in model.Resources.OfType<EntraAuthOpsResource>())
        {
            names.Add(provider.TenantIdParameter.Name);
            foreach (var app in provider.Apps.OfType<EntraAuthAppRegistrationResource>())
            {
                names.Add(app.TenantIdParameter.Name);
                names.Add(app.ClientIdParameter.Name);
                names.Add(app.ClientSecretParameter.Name);
            }
        }

        return names;
    }
}
