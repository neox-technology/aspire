using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Shows a one-shot dashboard notification inviting the user to create a client secret
/// when the secret resource is Waiting (secret missing) and InteractionService becomes available.
/// </summary>
internal sealed class EntraClientSecretNotificationCoordinator
{
    internal static readonly TimeSpan AvailabilityPollInterval = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan AvailabilityWaitTimeout = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly HashSet<string> _notifiedApps = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

#pragma warning disable ASPIREINTERACTION001
    /// <summary>
    /// Invites the user when the Auth app exists in Graph and the AppHost secret is empty.
    /// </summary>
    public void TryNotify(
        IServiceProvider services,
        EntraAuthAppRegistrationResource app,
        EntraAuthAppProbeResult probeResult,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(probeResult);
        _ = cancellationToken;

        if (!probeResult.Exists || probeResult.Error is not null)
        {
            return;
        }

        TryNotifyMissingSecret(services, app, probeResult.ObjectId);
    }

    /// <summary>
    /// Invites the user when the <c>{app}-clientsecret</c> resource is Waiting because the secret is unset
    /// (parent app Healthy). Retries until <see cref="IInteractionService.IsAvailable"/>.
    /// </summary>
    public void TryNotifyMissingSecret(
        IServiceProvider services,
        EntraAuthAppRegistrationResource app,
        string? applicationObjectId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Annotations.OfType<ClientSecretAnnotation>().Any())
        {
            return;
        }

        if (AuthParameterResolution.TryGetResolvedValue(app.ClientSecretParameter, services, out var secret)
            && !string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        lock (_gate)
        {
            if (_notifiedApps.Contains(app.Name) || !_inFlight.Add(app.Name))
            {
                return;
            }
        }

        var lifetime = services.GetService<IHostApplicationLifetime>();
        var notifyCt = lifetime?.ApplicationStopping ?? CancellationToken.None;

        _ = Task.Run(async () =>
        {
            var logger = services.GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(EntraClientSecretNotificationCoordinator));
            try
            {
                var interaction = await WaitForInteractionAsync(services, notifyCt, logger, app.Name)
                    .ConfigureAwait(false);
                if (interaction is null)
                {
                    return;
                }

                // Re-check secret in case it was set while waiting.
                if (AuthParameterResolution.TryGetResolvedValue(app.ClientSecretParameter, services, out var current)
                    && !string.IsNullOrWhiteSpace(current))
                {
                    return;
                }

                var result = await interaction.PromptNotificationAsync(
                        title: $"Client secret required — {app.Name}",
                        message:
                        $"Auth app '{app.DisplayName}' exists in Entra but has no client secret in AppHost. " +
                        "Create a password credential and save it to AppHost secrets?",
                        options: new NotificationInteractionOptions
                        {
                            Intent = MessageIntent.Warning,
                            PrimaryButtonText = "Create secret"
                        },
                        notifyCt)
                    .ConfigureAwait(false);

                lock (_gate)
                {
                    _notifiedApps.Add(app.Name);
                }

                if (!result.Data)
                {
                    return;
                }

                var (success, message) = await EntraClientSecretCreator.CreateInteractiveAsync(
                        app,
                        services,
                        applicationObjectId,
                        notifyCt)
                    .ConfigureAwait(false);

                if (success)
                {
                    logger?.LogInformation("{Message}", message);
                    var statusService = services.GetService<EntraAuthDashboardStatusService>();
                    if (statusService is not null)
                    {
                        await statusService.RefreshAsync(services, notifyCt).ConfigureAwait(false);
                    }
                }
                else
                {
                    logger?.LogWarning(
                        "Client secret create skipped/failed for '{App}': {Message}",
                        app.Name,
                        message);
                }
            }
            catch (OperationCanceledException)
            {
                // AppHost shutting down — allow retry on next run (do not mark notified).
            }
            catch (Exception ex)
            {
                logger?.LogWarning(
                    ex,
                    "Client secret notification failed for Auth app '{App}'. Will retry on next refresh.",
                    app.Name);
            }
            finally
            {
                lock (_gate)
                {
                    _inFlight.Remove(app.Name);
                }
            }
        }, CancellationToken.None);
    }

    private static async Task<IInteractionService?> WaitForInteractionAsync(
        IServiceProvider services,
        CancellationToken cancellationToken,
        ILogger? logger,
        string appName)
    {
        var deadline = DateTime.UtcNow + AvailabilityWaitTimeout;
        while (!cancellationToken.IsCancellationRequested)
        {
            var interaction = services.GetService<IInteractionService>();
            if (interaction is not null && interaction.IsAvailable)
            {
                return interaction;
            }

            if (DateTime.UtcNow >= deadline)
            {
                logger?.LogWarning(
                    "IInteractionService was not available within {Timeout} for Auth app '{App}'; skipping client-secret notification.",
                    AvailabilityWaitTimeout,
                    appName);
                return null;
            }

            try
            {
                await Task.Delay(AvailabilityPollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }
#pragma warning restore ASPIREINTERACTION001

    /// <summary>Test helper: clears notification dedupe state.</summary>
    internal void Reset()
    {
        lock (_gate)
        {
            _notifiedApps.Clear();
            _inFlight.Clear();
        }
    }
}
