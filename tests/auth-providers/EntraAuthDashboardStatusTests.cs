using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class EntraAuthDashboardStatusTests
{
    [Fact]
    public void EntraHierarchy_InitialState_IsWaiting()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();

        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("appregistration-api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var role = api.WithAppRoleExposition(
            AllowedMemberType.Applications, "Api.Caller", "Callers");

        var authOps = Assert.Single(builder.Resources.OfType<AuthOpsResource>());
        AssertWaiting(authOps, "AuthOps");
        AssertWaiting(entra.Resource.Resource, "AuthProvider");
        AssertWaiting(api.Resource, "EntraAuthAppRegistration");
        Assert.NotNull(scope);
        AssertWaiting(scope!.Resource, "AuthApiScope");
        AssertWaiting(role.Resource, "AuthAppRole");
    }

    [Fact]
    public void AppRegistration_HasProvisionAuthCommand()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.ProvisionCommandName);

        Assert.Equal("Provision app registration", command.DisplayName);
    }

    [Fact]
    public void PublisherApply_MapsWaitingHealthyUnhealthy()
    {
        var baseSnapshot = new CustomResourceSnapshot
        {
            ResourceType = "AuthOps",
            State = KnownResourceStates.NotStarted,
            Properties = []
        };

        var waiting = AuthDashboardStatusPublisher.Apply(baseSnapshot, AuthDashboardStatus.Waiting, null);
        Assert.Equal(KnownResourceStates.Waiting, waiting.State?.Text);
        Assert.Empty(waiting.HealthReports);

        var healthy = AuthDashboardStatusPublisher.Apply(baseSnapshot, AuthDashboardStatus.Healthy, "ok");
        Assert.Equal(KnownResourceStates.Running, healthy.State?.Text);
        Assert.Equal(HealthStatus.Healthy, healthy.HealthStatus);
        Assert.Contains(
            healthy.HealthReports,
            r => r.Name == AuthDashboardStatusPublisher.HealthReportName
                 && r.Status == HealthStatus.Healthy
                 && r.Description == "ok");

        var unhealthy = AuthDashboardStatusPublisher.Apply(
            baseSnapshot, AuthDashboardStatus.Unhealthy, "missing");
        Assert.Equal(KnownResourceStates.Running, unhealthy.State?.Text);
        Assert.Equal(HealthStatus.Unhealthy, unhealthy.HealthStatus);
        Assert.Contains(
            unhealthy.HealthReports,
            r => r.Name == AuthDashboardStatusPublisher.HealthReportName
                 && r.Status == HealthStatus.Unhealthy
                 && r.Description == "missing");
    }

    [Fact]
    public async Task StatusService_ClientIdUnset_AppAndChildrenWaiting()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });

        await using var services = CreateStatusHost(builder.Resources, new FakeEntraAuthHealthProbe());

        await AuthParameterValue.SetAsync(
            services,
            entra.Resource.Resource.TenantIdParameter,
            "tenant-1",
            CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(api.Resource.Name, out var appEvent));
        Assert.Equal(KnownResourceStates.Waiting, appEvent.Snapshot.State?.Text);

        Assert.NotNull(scope);
        Assert.True(notifications.TryGetCurrentState(scope!.Resource.Name, out var scopeEvent));
        Assert.Equal(KnownResourceStates.Waiting, scopeEvent.Snapshot.State?.Text);
    }

    [Fact]
    public async Task StatusService_ProbeMapsAppAndScopeHealth()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["client-1"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    ScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_as_user" },
                    AppRoleValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);

        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, api.Resource.ClientIdParameter, "client-1", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(api.Resource.Name, out var appEvent));
        Assert.Equal(KnownResourceStates.Running, appEvent.Snapshot.State?.Text);
        Assert.Equal(HealthStatus.Healthy, appEvent.Snapshot.HealthStatus);

        Assert.NotNull(scope);
        Assert.True(notifications.TryGetCurrentState(scope!.Resource.Name, out var scopeEvent));
        Assert.Equal(HealthStatus.Healthy, scopeEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_MissingScope_IsUnhealthy_WhileAppExists()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["client-1"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    ScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    AppRoleValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);

        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, api.Resource.ClientIdParameter, "client-1", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.NotNull(scope);
        Assert.True(notifications.TryGetCurrentState(scope!.Resource.Name, out var scopeEvent));
        Assert.Equal(KnownResourceStates.Running, scopeEvent.Snapshot.State?.Text);
        Assert.Equal(HealthStatus.Unhealthy, scopeEvent.Snapshot.HealthStatus);

        Assert.True(notifications.TryGetCurrentState(api.Resource.Name, out var appEvent));
        Assert.Equal(KnownResourceStates.Running, appEvent.Snapshot.State?.Text);
        Assert.Equal(HealthStatus.Unhealthy, appEvent.Snapshot.HealthStatus);
    }

    private static void AssertWaiting(IResource resource, string resourceType)
    {
        var initial = resource.Annotations.OfType<ResourceSnapshotAnnotation>().LastOrDefault()
            ?? throw new InvalidOperationException($"Missing ResourceSnapshotAnnotation on '{resource.Name}'.");
        Assert.Equal(resourceType, initial.InitialSnapshot.ResourceType);
        Assert.Equal(KnownResourceStates.Waiting, initial.InitialSnapshot.State?.Text);
    }

    private static ServiceProvider CreateStatusHost(
        IEnumerable<IResource> resources,
        IEntraAuthHealthProbe probe)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSingleton(new DistributedApplicationModel(resources.ToList()));
        services.AddSingleton<ResourceLoggerService>();
        services.AddSingleton<ResourceNotificationService>();
        services.AddSingleton(probe);
        services.AddSingleton<EntraAuthDashboardStatusService>();
        return services.BuildServiceProvider();
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public void StopApplication() { }
    }

    private sealed class FakeEntraAuthHealthProbe : IEntraAuthHealthProbe
    {
        public Dictionary<string, EntraAuthAppProbeResult> Results { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<EntraAuthAppProbeResult> ProbeAppAsync(string clientId, CancellationToken cancellationToken)
        {
            if (Results.TryGetValue(clientId, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(EntraAuthAppProbeResult.Missing());
        }
    }
}
