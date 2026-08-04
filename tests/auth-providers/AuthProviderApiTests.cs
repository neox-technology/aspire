#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthProviderApiTests
{
    [Fact]
    public void AddAuthProvider_Entra_AddAppRegistration_CreatesParametersAndResources()
    {
        var builder = DistributedApplication.CreateBuilder();

        var tenant = builder.AddParameter("entra-tenant-id", "11111111-1111-1111-1111-111111111111");
        var entra = builder.AddAuthProvider("entra")
            .Entra(o => o.TenantId = tenant);

        var web = entra.AddAppRegistration("web", "Test Web");

        Assert.Equal("entra", entra.Resource.Resource.ProviderSlug);
        Assert.IsType<EntraAuthOpsResource>(entra.Resource.Resource);
        Assert.Equal("web", web.Resource.Name);
        Assert.Equal("Test Web", web.Resource.DisplayName);
        Assert.Contains(builder.Resources.OfType<EntraAuthOpsResource>(), r => r.Name == "entra");
        Assert.Contains(builder.Resources.OfType<AuthOpsResource>(), r => r.Name == "auth-ops");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-tenant-id");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-web-client-id");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-web-client-secret");
        // Choice prompts live on dashboard Select commands / pipeline EnsureReadyAsync — not model-time.
        Assert.Empty(tenant.Resource.Annotations.OfType<InputGeneratorAnnotation>());

        var clientId = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "entra-web-client-id");
        Assert.Empty(clientId.Annotations.OfType<InputGeneratorAnnotation>());
        Assert.NotNull(clientId.Default);
        Assert.IsType<AuthDeferredParameterDefault>(clientId.Default);

        Assert.Contains(
            entra.Resource.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthProviderCommandExtensions.SelectTenantCommandName);
        Assert.Contains(
            web.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthAppRegistrationCommandExtensions.SelectAppRegistrationCommandName);
    }

    [Fact]
    public void WithAuth_AttachesAnnotationPipeline_WithoutThrowing()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var web = entra.AddAppRegistration("web", "Test Web");

        var api = builder.AddContainer("api", "mcr.microsoft.com/dotnet/runtime", "10.0");
        api.WithAuth(web);
        api.WithAuth(web, env =>
        {
            env.Section = "CustomAd";
            env.Map(AuthOutput.ClientId, "MY_CLIENT_ID");
        });

        Assert.NotNull(api.Resource);
        var wait = Assert.Single(api.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(web.Resource, wait.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);
    }

    [Fact]
    public void WithAuth_WaitsForAuthApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddAppRegistration("spa", "Spa");

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa);

        var wait = Assert.Single(ops.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(spa.Resource, wait.Resource);
    }

    [Fact]
    public void GetPrereqAppAuthStepName_UsesAppResourceName()
    {
        Assert.Equal("prereq-web-auth", AuthOpsExtensions.GetPrereqAppAuthStepName("web"));
    }

    [Fact]
    public void WithAuth_Default_EmitsAzureAdInstanceTenantAndClientId()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddAppRegistration("spa", "Spa");

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa);

        // Instance + TenantId + ClientId (no ClientSecret)
        Assert.Equal(3, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public void WithAuth_IncludeClientSecretTrue_EmitsSecret()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddAppRegistration("spa", "Spa");

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa, env => env.IncludeClientSecret = true);

        // Instance + TenantId + ClientId + ClientSecret
        Assert.Equal(4, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public void WithAuth_IncludeInstanceFalse_OmitsInstance()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddAppRegistration("spa", "Spa");

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa, env =>
        {
            env.IncludeInstance = false;
            env.Map(AuthOutput.TenantId, "VITE_ENTRA_TENANT_ID");
            env.Map(AuthOutput.ClientId, "VITE_ENTRA_CLIENT_ID");
        });

        Assert.Equal(2, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public void EntraAuthEnvOptions_Map_OverridesNames()
    {
        var options = new EntraAuthEnvOptions();
        options.Map(AuthOutput.ClientId, "MY_CLIENT_ID");
        Assert.Equal("MY_CLIENT_ID", options.ResolveName(AuthOutput.ClientId));
        Assert.Equal("AzureAd__TenantId", options.ResolveName(AuthOutput.TenantId));
    }

    [Fact]
    public void AuthEnvOptions_Map_OverridesNames()
    {
        var options = new AuthEnvOptions();
        options.Map(AuthOutput.ClientId, "MY_CLIENT_ID");
        Assert.Equal("MY_CLIENT_ID", options.ResolveName(AuthOutput.ClientId, "AUTH_ENTRA"));
        Assert.Equal("AUTH_ENTRA_TENANT_ID", options.ResolveName(AuthOutput.TenantId, "AUTH_ENTRA"));
    }

    [Fact]
    public void GetOrAddParameter_IsIdempotent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var first = AuthOpsExtensions.GetOrAddParameter(builder, "entra-tenant-id", "t1", secret: false);
        var second = AuthOpsExtensions.GetOrAddParameter(builder, "entra-tenant-id", "t2", secret: false);
        Assert.Same(first.Resource, second.Resource);
        Assert.Equal(1, builder.Resources.OfType<ParameterResource>().Count(p => p.Name == "entra-tenant-id"));
    }

    [Fact]
    public void StepNames_FollowContracts()
    {
        Assert.Equal(
            "prereq-auth-provider-entra-auth",
            AuthOpsExtensions.GetPrereqProviderAuthStepName("auth-provider-entra"));
        Assert.Equal(
            "prereq-auth-provider-entra-auth",
            EntraAuthOpsExtensions.GetPrereqStepName("auth-provider-entra"));
        Assert.Equal("prereq-providers-auth", AuthOpsExtensions.AuthPrereqProvidersStepName);
        Assert.Equal("deploy-auth", EntraAuthOpsExtensions.AuthDeployStepName);
        Assert.Equal("plan-web-auth", AuthOpsExtensions.GetPlanAuthStepName("web"));
        Assert.Equal("provision-web-auth", AuthOpsExtensions.GetProvisionAuthStepName("web"));
    }

    [Fact]
    public void Entra_AutoCreatesTenantParameter_WithDeferredDefaultAndSelectCommand()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("auth-provider-entra").Entra();

        var tenant = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "auth-provider-entra-tenant-id");
        Assert.Same(tenant, entra.Resource.Resource.TenantIdParameter);
        Assert.Empty(tenant.Annotations.OfType<InputGeneratorAnnotation>());
        Assert.NotNull(tenant.Default);
        Assert.IsType<AuthDeferredParameterDefault>(tenant.Default);
        Assert.Contains(
            entra.Resource.Resource.Annotations.OfType<ResourceCommandAnnotation>(),
            a => a.Name == EntraAuthProviderCommandExtensions.SelectTenantCommandName);
    }

    [Fact]
    public async Task RegistersPrereqFanInAndPlanDependsOnProvidersGate()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var tenant = builder.AddParameter(
            "auth-provider-entra-tenant-id",
            "11111111-1111-1111-1111-111111111111");
        var entra = builder.AddAuthProvider("auth-provider-entra")
            .Entra(o => o.TenantId = tenant);

        entra.AddAppRegistration("web", "Test Web");

        var steps = await CollectAuthOpsStepsAsync(builder);

        Assert.Single(builder.Resources.OfType<AuthOpsResource>());

        var prereqEntra = Assert.Single(steps, s => s.Name == "prereq-auth-provider-entra-auth");
        Assert.Contains("prereq-providers-auth", prereqEntra.RequiredBySteps);

        var prereqProviders = Assert.Single(steps, s => s.Name == "prereq-providers-auth");
        Assert.Empty(prereqProviders.DependsOnSteps);

        var plan = Assert.Single(steps, s => s.Name == "plan-web-auth");
        Assert.Contains("prereq-web-auth", plan.DependsOnSteps);
        Assert.DoesNotContain("prereq-providers-auth", plan.DependsOnSteps);
        Assert.DoesNotContain("prereq-auth-provider-entra-auth", plan.DependsOnSteps);

        var prereqApp = Assert.Single(steps, s => s.Name == "prereq-web-auth");
        Assert.Contains("prereq-providers-auth", prereqApp.DependsOnSteps);

        var provision = Assert.Single(steps, s => s.Name == "provision-web-auth");
        Assert.Contains("plan-web-auth", provision.DependsOnSteps);
        Assert.Contains("deploy-auth", provision.RequiredBySteps);

        var deploy = Assert.Single(steps, s => s.Name == "deploy-auth");
        Assert.Contains(WellKnownPipelineSteps.Deploy, deploy.RequiredBySteps);

        var clientId = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "auth-provider-entra-web-client-id");
        Assert.Empty(clientId.Annotations.OfType<InputGeneratorAnnotation>());
        Assert.NotNull(clientId.Default);
        Assert.IsType<AuthDeferredParameterDefault>(clientId.Default);
    }

    [Fact]
    public async Task WithApiPermission_ProvisionDependsOnExposerProvision()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var tenant = builder.AddParameter(
            "auth-provider-entra-tenant-id",
            "11111111-1111-1111-1111-111111111111");
        var entra = builder.AddAuthProvider("auth-provider-entra")
            .Entra(o => o.TenantId = tenant);

        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("api", "Test Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        _ = api;

        entra.AddAppRegistration("web", "Test Web")
            .WithApiPermission(scope!);

        var steps = await CollectAuthOpsStepsAsync(builder);
        var provisionWeb = Assert.Single(steps, s => s.Name == "provision-web-auth");
        Assert.Contains("plan-web-auth", provisionWeb.DependsOnSteps);
        Assert.Contains("provision-api-auth", provisionWeb.DependsOnSteps);
    }

    private static async Task<List<PipelineStep>> CollectAuthOpsStepsAsync(IDistributedApplicationBuilder builder)
    {
        var model = new DistributedApplicationModel([.. builder.Resources]);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var services = new ServiceCollection().BuildServiceProvider();
        var pipelineContext = new PipelineContext(
            model,
            executionContext,
            services,
            NullLogger.Instance,
            CancellationToken.None);

        var steps = new List<PipelineStep>();
        foreach (var resource in builder.Resources.Where(static r =>
                     r is AuthOpsResource or AuthOpsResourceBase or EntraAuthAppRegistrationResource))
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineStepAnnotation>())
            {
                var context = new PipelineStepFactoryContext
                {
                    Resource = resource,
                    PipelineContext = pipelineContext
                };
                var created = await annotation.CreateStepsAsync(context);
                steps.AddRange(created);
            }
        }

        return steps;
    }
}
