using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Computes and publishes Entra AuthOps dashboard statuses (worst-wins aggregation).
/// </summary>
internal sealed class EntraAuthDashboardStatusService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(12);

    private readonly object _gate = new();
    private readonly HashSet<string> _provisioningApps = new(StringComparer.Ordinal);
    private int _refreshing;

    public bool IsProvisioning(string appResourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appResourceName);
        lock (_gate)
        {
            return _provisioningApps.Contains(appResourceName);
        }
    }

    public void SetProvisioning(string appResourceName, bool provisioning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appResourceName);
        lock (_gate)
        {
            if (provisioning)
            {
                _provisioningApps.Add(appResourceName);
            }
            else
            {
                _provisioningApps.Remove(appResourceName);
            }
        }
    }

    public Task RefreshAsync(IServiceProvider services, CancellationToken cancellationToken) =>
        RefreshAsync(services, model: null, cancellationToken);

    public async Task RefreshAsync(
        IServiceProvider services,
        DistributedApplicationModel? model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (Interlocked.Exchange(ref _refreshing, 1) == 1)
        {
            return;
        }

        try
        {
            model ??= services.GetService<DistributedApplicationModel>();
            var notifications = services.GetService<ResourceNotificationService>();
            if (model is null || notifications is null)
            {
                return;
            }

            var probe = services.GetService<IEntraAuthHealthProbe>()
                ?? EntraGraphAuthHealthProbe.Create(services);
            var logger = services.GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(EntraAuthDashboardStatusService));

            var entraProviders = model.Resources.OfType<EntraAuthOpsResource>().ToList();
            if (entraProviders.Count == 0)
            {
                return;
            }

            var providerStatuses = new List<AuthDashboardStatus>(entraProviders.Count);

            foreach (var provider in entraProviders)
            {
                var providerStatus = await RefreshProviderAsync(
                        services,
                        notifications,
                        probe,
                        provider,
                        logger,
                        cancellationToken)
                    .ConfigureAwait(false);
                providerStatuses.Add(providerStatus);
            }

            var authOps = model.Resources.OfType<AuthOpsResource>().FirstOrDefault();
            if (authOps is not null)
            {
                var authOpsStatus = AuthStatusAggregator.WorstWins(providerStatuses);
                await AuthDashboardStatusPublisher.PublishAsync(
                        notifications,
                        authOps,
                        authOpsStatus,
                        description: $"Aggregated from {providerStatuses.Count} Entra provider(s).",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private static async Task<AuthDashboardStatus> RefreshProviderAsync(
        IServiceProvider services,
        ResourceNotificationService notifications,
        IEntraAuthHealthProbe probe,
        EntraAuthOpsResource provider,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (!AuthParameterResolution.TryGetResolvedValue(provider.TenantIdParameter, services, out var tenantId)
            || string.IsNullOrWhiteSpace(tenantId))
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    provider,
                    AuthDashboardStatus.Waiting,
                    description: "Waiting for tenant id.",
                    cancellationToken)
                .ConfigureAwait(false);

            // Children stay Waiting while tenant is unresolved.
            foreach (var app in provider.Apps.OfType<EntraAuthAppRegistrationResource>())
            {
                await PublishWaitingTreeAsync(notifications, app, "Waiting for provider tenant id.", cancellationToken)
                    .ConfigureAwait(false);
            }

            return AuthDashboardStatus.Waiting;
        }

        var appStatuses = new List<AuthDashboardStatus>();
        foreach (var app in provider.Apps.OfType<EntraAuthAppRegistrationResource>())
        {
            var appStatus = await RefreshAppAsync(
                    services,
                    notifications,
                    probe,
                    app,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);
            appStatuses.Add(appStatus);
        }

        var providerStatus = AuthStatusAggregator.WorstWins(
            appStatuses,
            whenEmpty: AuthDashboardStatus.Healthy);

        await AuthDashboardStatusPublisher.PublishAsync(
                notifications,
                provider,
                providerStatus,
                description: $"Tenant '{tenantId}' set; {appStatuses.Count} app registration(s).",
                cancellationToken)
            .ConfigureAwait(false);

        return providerStatus;
    }

    private static async Task<AuthDashboardStatus> RefreshAppAsync(
        IServiceProvider services,
        ResourceNotificationService notifications,
        IEntraAuthHealthProbe probe,
        EntraAuthAppRegistrationResource app,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var expositions = app.Annotations.OfType<ExposedApiAnnotation>()
            .Select(a => a.Exposition)
            .ToList();

        if (!AuthParameterResolution.TryGetResolvedValue(app.ClientIdParameter, services, out var clientId)
            || string.IsNullOrWhiteSpace(clientId)
            || EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId))
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    app,
                    AuthDashboardStatus.Waiting,
                    description: "Waiting for client id.",
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var exposition in expositions)
            {
                await AuthDashboardStatusPublisher.PublishAsync(
                        notifications,
                        exposition,
                        AuthDashboardStatus.Waiting,
                        description: "Waiting for parent app registration.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return AuthDashboardStatus.Waiting;
        }

        EntraAuthAppProbeResult probeResult;
        try
        {
            probeResult = await probe.ProbeAppAsync(clientId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Entra AuthOps Graph probe failed for app '{App}'.", app.Name);
            probeResult = EntraAuthAppProbeResult.Failed(ex.Message);
        }

        var ownStatus = probeResult.Exists && probeResult.Error is null
            ? AuthDashboardStatus.Healthy
            : AuthDashboardStatus.Unhealthy;

        var childStatuses = new List<AuthDashboardStatus>(expositions.Count);
        var parentHealthy = ownStatus == AuthDashboardStatus.Healthy;

        foreach (var exposition in expositions)
        {
            AuthDashboardStatus childStatus;
            string description;

            if (!parentHealthy)
            {
                childStatus = AuthDashboardStatus.Waiting;
                description = probeResult.Error is not null
                    ? $"Waiting: parent Graph probe failed ({probeResult.Error})."
                    : "Waiting for parent app registration to be healthy.";
            }
            else if (exposition is ScopeApiExposition scope)
            {
                var present = probeResult.ScopeValues.Contains(scope.ScopeValue);
                childStatus = present ? AuthDashboardStatus.Healthy : AuthDashboardStatus.Unhealthy;
                description = present
                    ? $"Scope '{scope.ScopeValue}' present in Graph."
                    : $"Scope '{scope.ScopeValue}' missing in Graph.";
            }
            else if (exposition is AppRoleApiExposition role)
            {
                var present = probeResult.AppRoleValues.Contains(role.Value);
                childStatus = present ? AuthDashboardStatus.Healthy : AuthDashboardStatus.Unhealthy;
                description = present
                    ? $"App role '{role.Value}' present in Graph."
                    : $"App role '{role.Value}' missing in Graph.";
            }
            else
            {
                childStatus = AuthDashboardStatus.Unhealthy;
                description = "Unknown exposition type.";
            }

            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    exposition,
                    childStatus,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);

            childStatuses.Add(childStatus);
        }

        // App status: own existence + children (worst-wins). Missing app is Unhealthy even if no children.
        var aggregatedChildren = AuthStatusAggregator.WorstWins(
            childStatuses,
            whenEmpty: AuthDashboardStatus.Healthy);
        var appStatus = AuthStatusAggregator.WorstWins([ownStatus, aggregatedChildren]);

        var appDescription = ownStatus == AuthDashboardStatus.Healthy
            ? $"App '{clientId}' found in Graph."
            : probeResult.Error ?? $"App '{clientId}' not found in Graph.";

        await AuthDashboardStatusPublisher.PublishAsync(
                notifications,
                app,
                appStatus,
                description: appDescription,
                cancellationToken)
            .ConfigureAwait(false);

        return appStatus;
    }

    private static async Task PublishWaitingTreeAsync(
        ResourceNotificationService notifications,
        EntraAuthAppRegistrationResource app,
        string description,
        CancellationToken cancellationToken)
    {
        await AuthDashboardStatusPublisher.PublishAsync(
                notifications,
                app,
                AuthDashboardStatus.Waiting,
                description,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var exposition in app.Annotations.OfType<ExposedApiAnnotation>().Select(a => a.Exposition))
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    exposition,
                    AuthDashboardStatus.Waiting,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
