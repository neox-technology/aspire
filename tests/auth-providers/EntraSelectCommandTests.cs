#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class EntraSelectCommandTests
{
    [Fact]
    public void TenantAndClientId_HaveEmptyPublishedDefault_SecretDoesNot()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        entra.AddAppRegistration("web", "Web");

        var tenant = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-tenant-id");
        var clientId = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-web-client-id");
        var clientSecret = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-web-client-secret");

        Assert.NotNull(tenant.Default);
        Assert.IsType<AuthDeferredParameterDefault>(tenant.Default);
        Assert.Equal(string.Empty, tenant.Default.GetDefaultValue());
        Assert.NotNull(clientId.Default);
        Assert.IsType<AuthDeferredParameterDefault>(clientId.Default);
        Assert.Equal(string.Empty, clientId.Default.GetDefaultValue());
        Assert.Null(clientSecret.Default);
    }

    [Fact]
    public void Provider_HasSelectTenantCommand_EnabledWhenTenantUnset()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();

        var command = Assert.Single(
            entra.Resource.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthProviderCommandExtensions.SelectTenantCommandName);
        Assert.Equal("Select tenant", command.DisplayName);

        using var sp = new ServiceCollection().BuildServiceProvider();
        var state = command.UpdateState(CreateUpdateContext(sp));
        Assert.Equal(ResourceCommandState.Enabled, state);
    }

    [Fact]
    public void SelectTenant_DisabledWhenTenantResolved()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();

        var command = Assert.Single(
            entra.Resource.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthProviderCommandExtensions.SelectTenantCommandName);

        using var sp = BuildServicesWithParameters(
            ("Parameters:provider-entra-tenant-id", "11111111-1111-1111-1111-111111111111"));

        var state = command.UpdateState(CreateUpdateContext(sp));
        Assert.Equal(ResourceCommandState.Disabled, state);
    }

    [Fact]
    public void AppRegistration_HasSelectAppCommand()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);
        Assert.Equal("Select or create app registration", command.DisplayName);
    }

    [Fact]
    public void SelectApp_DisabledWhenTenantUnset()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);

        using var sp = new ServiceCollection()
            .AddSingleton(new EntraAuthDashboardStatusService())
            .BuildServiceProvider();

        Assert.Equal(ResourceCommandState.Disabled, command.UpdateState(CreateUpdateContext(sp)));
    }

    [Fact]
    public void SelectApp_EnabledWhenTenantSet_NoWaitFor_ClientIdUnset()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);

        using var sp = BuildServicesWithParameters(
            ("Parameters:provider-entra-tenant-id", "11111111-1111-1111-1111-111111111111"));

        Assert.Equal(ResourceCommandState.Enabled, command.UpdateState(CreateUpdateContext(sp)));
    }

    [Fact]
    public void SelectApp_DisabledWhenClientIdResolved()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);

        using var sp = BuildServicesWithParameters(
            ("Parameters:provider-entra-tenant-id", "11111111-1111-1111-1111-111111111111"),
            ("Parameters:provider-entra-web-client-id", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        Assert.Equal(ResourceCommandState.Disabled, command.UpdateState(CreateUpdateContext(sp)));
    }

    [Fact]
    public void SelectApp_DisabledWhenWaitForExposerNotHealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = entra.AddAppRegistration("web", "Web").WithApiPermission(scope!);

        Assert.Contains(
            web.Resource.Annotations.OfType<WaitAnnotation>(),
            w => ReferenceEquals(w.Resource, api.Resource));

        var command = Assert.Single(
            web.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);

        var statusService = new EntraAuthDashboardStatusService();
        statusService.SetAppStatus(api.Resource.Name, AuthDashboardStatus.Waiting);

        var services = new ServiceCollection()
            .AddSingleton(statusService)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Parameters:provider-entra-tenant-id"] = "11111111-1111-1111-1111-111111111111"
                })
                .Build())
            .BuildServiceProvider();

        Assert.Equal(ResourceCommandState.Disabled, command.UpdateState(CreateUpdateContext(services)));
    }

    [Fact]
    public void SelectApp_EnabledWhenWaitForExposerHealthy()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = entra.AddAppRegistration("web", "Web").WithApiPermission(scope!);

        var command = Assert.Single(
            web.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);

        var statusService = new EntraAuthDashboardStatusService();
        statusService.SetAppStatus(api.Resource.Name, AuthDashboardStatus.Healthy);

        var services = new ServiceCollection()
            .AddSingleton(statusService)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Parameters:provider-entra-tenant-id"] = "11111111-1111-1111-1111-111111111111"
                })
                .Build())
            .BuildServiceProvider();

        Assert.Equal(ResourceCommandState.Enabled, command.UpdateState(CreateUpdateContext(services)));
    }

    [Fact]
    public void Provision_EnabledWhenWaitingAndExplicitCreateSentinel()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.ProvisionCommandName);

        var statusService = new EntraAuthDashboardStatusService();
        statusService.SetAppStatus(app.Resource.Name, AuthDashboardStatus.Waiting);

        using var sp = BuildServicesWithParameters(
            statusService,
            ("Parameters:provider-entra-tenant-id", "11111111-1111-1111-1111-111111111111"),
            ("Parameters:provider-entra-web-client-id", EntraAppRegistrationParameterPrompt.CreateSentinel));

        Assert.Equal(ResourceCommandState.Enabled, command.UpdateState(CreateUpdateContext(sp)));
    }

    [Fact]
    public void Provision_DisabledWhenWaitingWithoutCreateSentinel()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        var app = entra.AddAppRegistration("web", "Web");

        var command = Assert.Single(
            app.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.ProvisionCommandName);

        var statusService = new EntraAuthDashboardStatusService();
        statusService.SetAppStatus(app.Resource.Name, AuthDashboardStatus.Waiting);

        using var sp = BuildServicesWithParameters(
            statusService,
            ("Parameters:provider-entra-tenant-id", "11111111-1111-1111-1111-111111111111"));

        Assert.Equal(ResourceCommandState.Disabled, command.UpdateState(CreateUpdateContext(sp)));
    }

    private static UpdateCommandStateContext CreateUpdateContext(IServiceProvider sp) =>
        new()
        {
            ResourceSnapshot = new CustomResourceSnapshot
            {
                ResourceType = "EntraAuth",
                Properties = []
            },
            ServiceProvider = sp
        };

    private static ServiceProvider BuildServicesWithParameters(params (string Key, string Value)[] values) =>
        BuildServicesWithParameters(new EntraAuthDashboardStatusService(), values);

    private static ServiceProvider BuildServicesWithParameters(
        EntraAuthDashboardStatusService statusService,
        params (string Key, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Key, v => (string?)v.Value, StringComparer.Ordinal);
        return new ServiceCollection()
            .AddSingleton(statusService)
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(map).Build())
            .BuildServiceProvider();
    }
}
