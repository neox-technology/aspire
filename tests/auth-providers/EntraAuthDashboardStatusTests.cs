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
        var web = entra.AddAppRegistration("appregistration-web", "Web")
            .WithApiPermission(scope!)
            .WithApiPermission(MicrosoftGraph.Delegated.UserRead);

        var authOps = Assert.Single(builder.Resources.OfType<AuthOpsResource>());
        AssertWaiting(authOps, "AuthOps");
        AssertWaiting(entra.Resource.Resource, "AuthProvider");
        AssertWaiting(api.Resource, "EntraAuthAppRegistration");
        Assert.NotNull(scope);
        AssertWaiting(scope!.Resource, "AuthApiScope");
        AssertWaiting(role.Resource, "AuthAppRole");

        var inModelPerm = Assert.Single(
            web.Resource.Annotations.OfType<ApiPermissionAnnotation>()).PermissionResource;
        var graphPerm = Assert.Single(
            web.Resource.Annotations.OfType<WellKnownApiPermissionAnnotation>()).PermissionResource;
        AssertWaiting(inModelPerm, "AuthApiPermission");
        AssertWaiting(graphPerm, "AuthApiPermission");
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
    public void AppRegistration_HasHealthCheckAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var healthCheck = Assert.Single(app.Resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.Equal(EntraAuthAppRegistrationHealthCheck.GetKey("web"), healthCheck.Key);
    }

    [Fact]
    public async Task HealthCheck_MapsCachedDashboardStatus()
    {
        var statusService = new EntraAuthDashboardStatusService();
        var healthCheck = new EntraAuthAppRegistrationHealthCheck("web", statusService);
        var context = new HealthCheckContext();

        var unknown = await healthCheck.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Unhealthy, unknown.Status);

        statusService.SetAppStatus("web", AuthDashboardStatus.Waiting);
        var waiting = await healthCheck.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Unhealthy, waiting.Status);

        statusService.SetAppStatus("web", AuthDashboardStatus.Unhealthy);
        var unhealthy = await healthCheck.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Unhealthy, unhealthy.Status);

        statusService.SetAppStatus("web", AuthDashboardStatus.Healthy);
        var healthy = await healthCheck.CheckHealthAsync(context);
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
    }

    [Fact]
    public async Task StatusService_Refresh_CachesAppStatusForHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var api = entra.AddAppRegistration("api", "Api");

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

        Assert.True(statusService.TryGetAppStatus(api.Resource.Name, out var cached));
        Assert.Equal(AuthDashboardStatus.Healthy, cached);

        var healthCheck = new EntraAuthAppRegistrationHealthCheck(api.Resource.Name, statusService);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
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

    [Fact]
    public async Task StatusService_ApiPermission_Present_IsHealthy_WhenGraphIdsDifferFromModel()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!);

        var scopePerm = Assert.Single(web.Resource.Annotations.OfType<ApiPermissionAnnotation>());
        var graphScopeId = Guid.Parse("648a9683-c59e-4995-a45f-27b88697311f");
        Assert.NotEqual(graphScopeId, scope!.Resource.PermissionId);

        var graphKey = EntraApiPermissionApplicator.FormatKey(new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = "api-client",
            PermissionId = graphScopeId,
            Type = "Scope"
        });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["api-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    ScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_as_user" },
                    ScopeIdsByValue = new Dictionary<string, Guid>(StringComparer.Ordinal)
                    {
                        ["access_as_user"] = graphScopeId
                    }
                },
                ["web-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    RequiredResourceAccessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        graphKey
                    }
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, api.Resource.ClientIdParameter, "api-client", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "web-client", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(scopePerm.PermissionResource.Name, out var scopePermEvent));
        Assert.Equal(HealthStatus.Healthy, scopePermEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_ApiPermission_Present_IsHealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!)
            .WithApiPermission(MicrosoftGraph.Delegated.UserRead);

        var scopePerm = Assert.Single(web.Resource.Annotations.OfType<ApiPermissionAnnotation>());
        var graphPerm = Assert.Single(web.Resource.Annotations.OfType<WellKnownApiPermissionAnnotation>());
        var scopeKey = EntraApiPermissionApplicator.FormatKey(new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = "api-client",
            PermissionId = scope!.Resource.PermissionId,
            Type = "Scope"
        });
        var graphKey = EntraApiPermissionApplicator.FormatKey(new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = MicrosoftGraph.AppId,
            PermissionId = MicrosoftGraph.Delegated.UserRead.PermissionId,
            Type = "Scope"
        });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["api-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    ScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_as_user" }
                },
                ["web-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    RequiredResourceAccessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        scopeKey,
                        graphKey
                    }
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, api.Resource.ClientIdParameter, "api-client", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "web-client", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(scopePerm.PermissionResource.Name, out var scopePermEvent));
        Assert.Equal(HealthStatus.Healthy, scopePermEvent.Snapshot.HealthStatus);
        Assert.True(notifications.TryGetCurrentState(graphPerm.PermissionResource.Name, out var graphPermEvent));
        Assert.Equal(HealthStatus.Healthy, graphPermEvent.Snapshot.HealthStatus);
        Assert.True(notifications.TryGetCurrentState(web.Resource.Name, out var webEvent));
        Assert.Equal(HealthStatus.Healthy, webEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_ApiPermission_ExposerUnhealthy_ConsumerUnhealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!);

        var scopeKey = EntraApiPermissionApplicator.FormatKey(new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = "api-client",
            PermissionId = scope!.Resource.PermissionId,
            Type = "Scope"
        });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                // Exposer missing in Graph → Unhealthy
                ["api-client"] = new EntraAuthAppProbeResult { Exists = false },
                ["web-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    RequiredResourceAccessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        scopeKey
                    }
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, api.Resource.ClientIdParameter, "api-client", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "web-client", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(api.Resource.Name, out var apiEvent));
        Assert.Equal(HealthStatus.Unhealthy, apiEvent.Snapshot.HealthStatus);

        Assert.True(notifications.TryGetCurrentState(web.Resource.Name, out var webEvent));
        Assert.Equal(KnownResourceStates.Running, webEvent.Snapshot.State?.Text);
        Assert.Equal(HealthStatus.Unhealthy, webEvent.Snapshot.HealthStatus);
        Assert.True(statusService.TryGetAppStatus(web.Resource.Name, out var cached));
        Assert.Equal(AuthDashboardStatus.Unhealthy, cached);
    }

    [Fact]
    public async Task StatusService_ApiPermission_ExposerWaiting_ConsumerWaiting()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        // Register consumer before exposer to prove two-pass order independence.
        var webBuilder = entra.AddAppRegistration("web", "Web");
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = webBuilder.WithApiPermission(scope!);

        var scopeKey = EntraApiPermissionApplicator.FormatKey(new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = "api-client",
            PermissionId = scope!.Resource.PermissionId,
            Type = "Scope"
        });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["api-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    ScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_as_user" }
                },
                ["web-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    RequiredResourceAccessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        scopeKey
                    }
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        // Exposer ClientId unset → Waiting; consumer fully resolvable otherwise.
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "web-client", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(api.Resource.Name, out var apiEvent));
        Assert.Equal(KnownResourceStates.Waiting, apiEvent.Snapshot.State?.Text);

        Assert.True(notifications.TryGetCurrentState(web.Resource.Name, out var webEvent));
        Assert.Equal(KnownResourceStates.Waiting, webEvent.Snapshot.State?.Text);
        Assert.True(statusService.TryGetAppStatus(web.Resource.Name, out var cached));
        Assert.Equal(AuthDashboardStatus.Waiting, cached);
    }

    [Fact]
    public async Task StatusService_ApiPermission_SelfOwned_DoesNotGateOnSelf()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            })
            .WithApiPermission(scope!);

        var scopeKey = EntraApiPermissionApplicator.FormatKey(new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = "api-client",
            PermissionId = scope!.Resource.PermissionId,
            Type = "Scope"
        });

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["api-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    ScopeValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "access_as_user" },
                    RequiredResourceAccessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        scopeKey
                    }
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, api.Resource.ClientIdParameter, "api-client", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(api.Resource.Name, out var apiEvent));
        Assert.Equal(HealthStatus.Healthy, apiEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_ApiPermission_Missing_IsUnhealthy_AndAppUnhealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(MicrosoftGraph.Delegated.UserRead);

        var graphPerm = Assert.Single(web.Resource.Annotations.OfType<WellKnownApiPermissionAnnotation>());

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["web-client"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    RequiredResourceAccessKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "web-client", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(graphPerm.PermissionResource.Name, out var permEvent));
        Assert.Equal(KnownResourceStates.Running, permEvent.Snapshot.State?.Text);
        Assert.Equal(HealthStatus.Unhealthy, permEvent.Snapshot.HealthStatus);

        Assert.True(notifications.TryGetCurrentState(web.Resource.Name, out var webEvent));
        Assert.Equal(HealthStatus.Unhealthy, webEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_RedirectUrisMatch_AppHealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var web = entra.AddAppRegistration("web", "Web")
            .WithLocalhostRedirectUri(AuthApplicationType.Web, 7281, "/signin-oidc");

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["client-1"] = new EntraAuthAppProbeResult
                {
                    Exists = true,
                    RedirectUris =
                    [
                        new AuthDesiredRedirectUri
                        {
                            Type = AuthApplicationType.Web,
                            Uri = "https://localhost:7281/signin-oidc"
                        }
                    ]
                }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "client-1", CancellationToken.None);

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(web.Resource.Name, out var appEvent));
        Assert.Equal(HealthStatus.Healthy, appEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_RedirectUrisMismatch_AppUnhealthy_ScopeStillHealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithLocalhostRedirectUri(AuthApplicationType.Web, 7281, "/signin-oidc")
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
                    RedirectUris = [] // desired localhost missing in Graph
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
        Assert.Equal(HealthStatus.Unhealthy, appEvent.Snapshot.HealthStatus);
        Assert.Contains(
            appEvent.Snapshot.HealthReports,
            r => r.Description == "Redirect URIs differ from Graph.");

        Assert.NotNull(scope);
        Assert.True(notifications.TryGetCurrentState(scope!.Resource.Name, out var scopeEvent));
        Assert.Equal(HealthStatus.Healthy, scopeEvent.Snapshot.HealthStatus);
    }

    [Fact]
    public async Task StatusService_RedirectUriParameterUnresolved_AppWaiting()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var baseUrl = builder.AddParameter("redirect-base");
        var web = entra.AddAppRegistration("web", "Web")
            .WithRedirectUri(AuthApplicationType.Web, baseUrl, "/callback");

        var probe = new FakeEntraAuthHealthProbe
        {
            Results =
            {
                ["client-1"] = new EntraAuthAppProbeResult { Exists = true }
            }
        };

        await using var services = CreateStatusHost(builder.Resources, probe);
        await AuthParameterValue.SetAsync(
            services, entra.Resource.Resource.TenantIdParameter, "tenant-1", CancellationToken.None);
        await AuthParameterValue.SetAsync(
            services, web.Resource.ClientIdParameter, "client-1", CancellationToken.None);
        // Intentionally do not set redirect-base.

        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var model = services.GetRequiredService<DistributedApplicationModel>();
        await statusService.RefreshAsync(services, model, CancellationToken.None);

        var notifications = services.GetRequiredService<ResourceNotificationService>();
        Assert.True(notifications.TryGetCurrentState(web.Resource.Name, out var appEvent));
        Assert.Equal(KnownResourceStates.Waiting, appEvent.Snapshot.State?.Text);
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
