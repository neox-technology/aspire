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
    private readonly Dictionary<string, AuthDashboardStatus> _appStatuses =
        new(StringComparer.Ordinal);
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

    /// <summary>
    /// Last published dashboard status for an Entra app registration (WaitFor health check source).
    /// </summary>
    public bool TryGetAppStatus(string appResourceName, out AuthDashboardStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appResourceName);
        lock (_gate)
        {
            return _appStatuses.TryGetValue(appResourceName, out status);
        }
    }

    internal void SetAppStatus(string appResourceName, AuthDashboardStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appResourceName);
        lock (_gate)
        {
            _appStatuses[appResourceName] = status;
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
                        this,
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
        EntraAuthDashboardStatusService statusService,
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
                await PublishWaitingTreeAsync(
                        statusService,
                        notifications,
                        app,
                        "Waiting for provider tenant id.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return AuthDashboardStatus.Waiting;
        }

        var apps = provider.Apps.OfType<EntraAuthAppRegistrationResource>().ToList();

        // Pass 1: own Graph/children status (order-independent; no final app publish yet).
        var computed = new Dictionary<string, AppOwnStatusResult>(StringComparer.Ordinal);
        foreach (var app in apps)
        {
            var own = await ComputeAppOwnStatusAsync(
                    services,
                    notifications,
                    probe,
                    app,
                    logger,
                    cancellationToken)
                .ConfigureAwait(false);
            computed[app.Name] = own;
        }

        // Pass 2: worst-wins with in-model WithApiPermission exposers, then publish apps.
        var appStatuses = new List<AuthDashboardStatus>(apps.Count);
        foreach (var app in apps)
        {
            var own = computed[app.Name];
            var statuses = new List<AuthDashboardStatus> { own.Status };
            foreach (var exposer in CollectDistinctInModelExposers(app))
            {
                AuthDashboardStatus exposerStatus;
                if (computed.TryGetValue(exposer.Name, out var exposerOwn))
                {
                    exposerStatus = exposerOwn.Status;
                }
                else if (statusService.TryGetAppStatus(exposer.Name, out var cached))
                {
                    exposerStatus = cached;
                }
                else
                {
                    exposerStatus = AuthDashboardStatus.Waiting;
                }

                // Exposer Unhealthy is a dependency block for the consumer (Waiting), not a
                // consumer Graph failure (Unhealthy).
                if (exposerStatus == AuthDashboardStatus.Unhealthy)
                {
                    exposerStatus = AuthDashboardStatus.Waiting;
                }

                statuses.Add(exposerStatus);
            }

            var appStatus = AuthStatusAggregator.WorstWins(statuses);
            var description = own.Description;
            if (appStatus != own.Status)
            {
                description = appStatus switch
                {
                    AuthDashboardStatus.Waiting =>
                        "Waiting for in-model WithApiPermission exposer Auth app(s).",
                    _ => own.Description
                };
            }

            statusService.SetAppStatus(app.Name, appStatus);
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    app,
                    appStatus,
                    description: description,
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

    private readonly record struct AppOwnStatusResult(AuthDashboardStatus Status, string? Description);

    /// <summary>
    /// Computes own Graph + children status and publishes children; does not publish the app
    /// (provider pass 2 applies exposer gating then publishes).
    /// </summary>
    private static async Task<AppOwnStatusResult> ComputeAppOwnStatusAsync(
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
        var apiPermissions = CollectApiPermissionResources(app);

        if (!AuthParameterResolution.TryGetResolvedValue(app.ClientIdParameter, services, out var clientId)
            || string.IsNullOrWhiteSpace(clientId)
            || EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId))
        {
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

            foreach (var permission in apiPermissions)
            {
                await AuthDashboardStatusPublisher.PublishAsync(
                        notifications,
                        permission,
                        AuthDashboardStatus.Waiting,
                        description: "Waiting for parent app registration.",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await PublishClientSecretStatusAsync(
                    services,
                    notifications,
                    app,
                    parentHealthy: false,
                    waitingForClientId: true,
                    applicationObjectId: null,
                    cancellationToken)
                .ConfigureAwait(false);

            return new AppOwnStatusResult(AuthDashboardStatus.Waiting, "Waiting for client id.");
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

        services.GetService<EntraClientSecretNotificationCoordinator>()
            ?.TryNotify(services, app, probeResult, cancellationToken);

        var graphExists = probeResult.Exists && probeResult.Error is null;
        var ownStatus = graphExists
            ? AuthDashboardStatus.Healthy
            : AuthDashboardStatus.Unhealthy;
        string? ownDescription = graphExists
            ? $"App '{clientId}' found in Graph."
            : probeResult.Error ?? $"App '{clientId}' not found in Graph.";

        if (graphExists)
        {
            IReadOnlyList<AuthDesiredRedirectUri> desiredRedirects;
            try
            {
                desiredRedirects = await EntraRedirectUriApplicator.ResolveAsync(app, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogDebug(
                    ex,
                    "Entra AuthOps redirect URI resolve pending for app '{App}'.",
                    app.Name);

                await PublishWaitingChildrenAsync(
                        notifications,
                        app,
                        $"Waiting for redirect URI parameter: {ex.Message}",
                        cancellationToken)
                    .ConfigureAwait(false);

                return new AppOwnStatusResult(
                    AuthDashboardStatus.Waiting,
                    $"Waiting for redirect URI parameter: {ex.Message}");
            }

            if (EntraRedirectUriApplicator.Differ(desiredRedirects, probeResult.RedirectUris))
            {
                ownStatus = AuthDashboardStatus.Unhealthy;
                ownDescription = "Redirect URIs differ from Graph.";
            }
        }

        var childStatuses = new List<AuthDashboardStatus>(expositions.Count + apiPermissions.Count);
        // Gate expositions/permissions on Graph existence only — redirect mismatch must not force children Waiting.
        var parentExists = graphExists;
        var exposerProbeCache = new Dictionary<string, EntraAuthAppProbeResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var exposition in expositions)
        {
            AuthDashboardStatus childStatus;
            string description;

            if (!parentExists)
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

        foreach (var permission in apiPermissions)
        {
            AuthDashboardStatus childStatus;
            string description;

            if (!parentExists)
            {
                childStatus = AuthDashboardStatus.Waiting;
                description = probeResult.Error is not null
                    ? $"Waiting: parent Graph probe failed ({probeResult.Error})."
                    : "Waiting for parent app registration to be healthy.";
            }
            else if (!EntraApiPermissionApplicator.TryResolveDesired(
                         permission,
                         exposer => ResolveExposerClientId(services, exposer),
                         out var desired))
            {
                childStatus = AuthDashboardStatus.Waiting;
                description = $"Waiting for exposer ClientId for permission '{permission.Value}'.";
            }
            else
            {
                EntraAuthAppProbeResult? exposerProbe = null;
                if (permission.Exposition?.Owner is { } exposer
                    && ResolveExposerClientId(services, exposer) is { } exposerClientId)
                {
                    if (!exposerProbeCache.TryGetValue(exposerClientId, out exposerProbe))
                    {
                        try
                        {
                            exposerProbe = await probe.ProbeAppAsync(exposerClientId, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            exposerProbe = EntraAuthAppProbeResult.Failed(ex.Message);
                        }

                        exposerProbeCache[exposerClientId] = exposerProbe;
                    }

                    if (!IsExposerExpositionHealthy(permission, exposerProbe, out var waitReason))
                    {
                        childStatus = AuthDashboardStatus.Waiting;
                        description = waitReason;
                    }
                    else
                    {
                        desired = EntraApiPermissionApplicator.RemapDesiredWithExposerProbe(
                            permission,
                            desired,
                            exposerProbe);

                        var desiredKey = EntraApiPermissionApplicator.FormatKey(desired);
                        var present = probeResult.RequiredResourceAccessKeys.Contains(desiredKey);
                        childStatus = present ? AuthDashboardStatus.Healthy : AuthDashboardStatus.Unhealthy;
                        description = present
                            ? $"API permission '{permission.Value}' present in Graph."
                            : $"API permission '{permission.Value}' missing in Graph.";
                    }
                }
                else
                {
                    var desiredKey = EntraApiPermissionApplicator.FormatKey(desired);
                    var present = probeResult.RequiredResourceAccessKeys.Contains(desiredKey);
                    childStatus = present ? AuthDashboardStatus.Healthy : AuthDashboardStatus.Unhealthy;
                    description = present
                        ? $"API permission '{permission.Value}' present in Graph."
                        : $"API permission '{permission.Value}' missing in Graph.";
                }
            }

            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    permission,
                    childStatus,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);

            childStatuses.Add(childStatus);
        }

        // Client secret status is independent of parent worst-wins aggregation.
        await PublishClientSecretStatusAsync(
                services,
                notifications,
                app,
                parentHealthy: parentExists,
                waitingForClientId: false,
                applicationObjectId: probeResult.ObjectId,
                cancellationToken)
            .ConfigureAwait(false);

        // Own status: existence + redirect match + children (worst-wins). Exposer gate is pass 2.
        var aggregatedChildren = AuthStatusAggregator.WorstWins(
            childStatuses,
            whenEmpty: AuthDashboardStatus.Healthy);
        var appStatus = AuthStatusAggregator.WorstWins([ownStatus, aggregatedChildren]);

        return new AppOwnStatusResult(appStatus, ownDescription);
    }

    private static IEnumerable<EntraAuthAppRegistrationResource> CollectDistinctInModelExposers(
        EntraAuthAppRegistrationResource app)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var annotation in app.Annotations.OfType<ApiPermissionAnnotation>())
        {
            if (annotation.PermissionResource.Exposition?.Owner is not { } exposer
                || ReferenceEquals(exposer, app)
                || !seen.Add(exposer.Name))
            {
                continue;
            }

            yield return exposer;
        }
    }

    /// <summary>
    /// In-model permissions wait until the exposer's matching scope/role exists in Graph.
    /// </summary>
    private static bool IsExposerExpositionHealthy(
        ApiPermissionResource permission,
        EntraAuthAppProbeResult exposerProbe,
        out string waitReason)
    {
        if (!exposerProbe.Exists || exposerProbe.Error is not null)
        {
            waitReason = exposerProbe.Error is not null
                ? $"Waiting for exposer Graph probe ({exposerProbe.Error})."
                : "Waiting for exposer app registration in Graph.";
            return false;
        }

        switch (permission.Exposition)
        {
            case ScopeApiExposition scope when !exposerProbe.ScopeValues.Contains(scope.ScopeValue):
                waitReason = $"Waiting for exposer scope '{scope.ScopeValue}'.";
                return false;
            case AppRoleApiExposition role when !exposerProbe.AppRoleValues.Contains(role.Value):
                waitReason = $"Waiting for exposer app role '{role.Value}'.";
                return false;
            default:
                waitReason = string.Empty;
                return true;
        }
    }

    private static async Task PublishWaitingChildrenAsync(
        ResourceNotificationService notifications,
        EntraAuthAppRegistrationResource app,
        string description,
        CancellationToken cancellationToken)
    {
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

        foreach (var permission in CollectApiPermissionResources(app))
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    permission,
                    AuthDashboardStatus.Waiting,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var secretResource = app.Annotations.OfType<ClientSecretAnnotation>().LastOrDefault()?.SecretResource;
        if (secretResource is not null)
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    secretResource,
                    AuthDashboardStatus.Waiting,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task PublishClientSecretStatusAsync(
        IServiceProvider services,
        ResourceNotificationService notifications,
        EntraAuthAppRegistrationResource app,
        bool parentHealthy,
        bool waitingForClientId,
        string? applicationObjectId,
        CancellationToken cancellationToken)
    {
        var secretAnnotation = app.Annotations.OfType<ClientSecretAnnotation>().LastOrDefault();
        if (secretAnnotation is null)
        {
            return;
        }

        var secretResource = secretAnnotation.SecretResource;
        AuthDashboardStatus status;
        string description;

        if (waitingForClientId)
        {
            status = AuthDashboardStatus.Waiting;
            description = "Waiting for client id.";
        }
        else if (!parentHealthy)
        {
            status = AuthDashboardStatus.Waiting;
            description = "Waiting for parent app registration to be healthy.";
        }
        else if (AuthParameterResolution.TryGetResolvedValue(secretResource.Parameter, services, out var secret)
                 && !string.IsNullOrWhiteSpace(secret))
        {
            status = AuthDashboardStatus.Healthy;
            description = "Client secret is set in AppHost.";
        }
        else
        {
            status = AuthDashboardStatus.Waiting;
            description = "Client secret not set — use Create client secret.";
            // Notify as soon as the secret resource is Waiting with conditions met (parent Healthy).
            services.GetService<EntraClientSecretNotificationCoordinator>()
                ?.TryNotifyMissingSecret(services, app, applicationObjectId);
        }

        await AuthDashboardStatusPublisher.PublishAsync(
                notifications,
                secretResource,
                status,
                description,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task PublishWaitingTreeAsync(
        EntraAuthDashboardStatusService statusService,
        ResourceNotificationService notifications,
        EntraAuthAppRegistrationResource app,
        string description,
        CancellationToken cancellationToken)
    {
        statusService.SetAppStatus(app.Name, AuthDashboardStatus.Waiting);
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

        foreach (var permission in CollectApiPermissionResources(app))
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    permission,
                    AuthDashboardStatus.Waiting,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var secretResource = app.Annotations.OfType<ClientSecretAnnotation>().LastOrDefault()?.SecretResource;
        if (secretResource is not null)
        {
            await AuthDashboardStatusPublisher.PublishAsync(
                    notifications,
                    secretResource,
                    AuthDashboardStatus.Waiting,
                    description,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static List<ApiPermissionResource> CollectApiPermissionResources(
        EntraAuthAppRegistrationResource app)
    {
        var result = new List<ApiPermissionResource>();
        result.AddRange(app.Annotations.OfType<ApiPermissionAnnotation>().Select(a => a.PermissionResource));
        result.AddRange(
            app.Annotations.OfType<WellKnownApiPermissionAnnotation>().Select(a => a.PermissionResource));
        return result;
    }

    private static string? ResolveExposerClientId(
        IServiceProvider services,
        EntraAuthAppRegistrationResource exposer)
    {
        if (!AuthParameterResolution.TryGetResolvedValue(exposer.ClientIdParameter, services, out var clientId)
            || string.IsNullOrWhiteSpace(clientId)
            || EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId))
        {
            return null;
        }

        return clientId;
    }
}
