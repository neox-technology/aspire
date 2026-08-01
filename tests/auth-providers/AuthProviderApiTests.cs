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
    public void AddAuthProvider_Entra_AddApp_CreatesParametersAndResources()
    {
        var builder = DistributedApplication.CreateBuilder();

        var tenant = builder.AddParameter("entra-tenant-id", "11111111-1111-1111-1111-111111111111");
        var entra = builder.AddAuthProvider("entra")
            .Entra(o => o.TenantId = tenant);

        var web = entra.AddApp("web", o =>
        {
            o.DisplayName = "Test Web";
            o.ApplicationType = AuthApplicationType.Web;
            o.RedirectUris = ["https://localhost/signin-oidc"];
            o.CreateClientSecret = true;
        });

        Assert.Equal("entra", entra.Resource.Resource.ProviderSlug);
        Assert.IsType<EntraAuthOpsResource>(entra.Resource.Resource);
        Assert.Equal("web", web.Resource.Name);
        Assert.Contains(builder.Resources.OfType<EntraAuthOpsResource>(), r => r.Name == "entra");
        Assert.Contains(builder.Resources.OfType<AuthOpsResource>(), r => r.Name == "auth-ops");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-tenant-id");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-web-client-id");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-web-client-secret");
        Assert.Contains(tenant.Resource.Annotations.OfType<InputGeneratorAnnotation>(), _ => true);
    }

    [Fact]
    public void WithAuth_AttachesAnnotationPipeline_WithoutThrowing()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var web = entra.AddApp("web", o =>
        {
            o.RedirectUris = ["https://localhost/cb"];
            o.ExistingClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
            o.CreateClientSecret = false;
        });

        var api = builder.AddContainer("api", "mcr.microsoft.com/dotnet/runtime", "10.0");
        api.WithAuth(web);
        api.WithAuth(web, env =>
        {
            env.Prefix = "CUSTOM";
            env.Map(AuthOutput.ClientId, "MY_CLIENT_ID");
            env.IncludeRedirectUri = true;
        });

        Assert.NotNull(api.Resource);
    }

    [Fact]
    public void AuthApp_DefaultEnvPrefix_SingleApp_IsShortForm()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var web = entra.AddApp("web", o => o.RedirectUris = ["https://localhost/cb"]);

        Assert.Equal("AUTH_ENTRA", web.Resource.DefaultEnvPrefix);
    }

    [Fact]
    public void WithAuth_SpaWithoutCreateClientSecret_SkipsClientSecretEnv()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddApp("spa", o =>
        {
            o.ApplicationType = AuthApplicationType.Spa;
            o.RedirectUris = ["http://localhost:5173"];
            o.CreateClientSecret = false;
        });

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa);

        // TenantId + ClientId + Authority (no ClientSecret)
        Assert.Equal(3, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public void WithAuth_IncludeClientSecretOverride_EmitsSecretEvenWhenCreateFalse()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddApp("spa", o =>
        {
            o.ApplicationType = AuthApplicationType.Spa;
            o.RedirectUris = ["http://localhost:5173"];
            o.CreateClientSecret = false;
        });

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa, env => env.IncludeClientSecret = true);

        // TenantId + ClientId + ClientSecret + Authority
        Assert.Equal(4, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public void AuthApp_DefaultEnvPrefix_MultiApp_IncludesAppSlug()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var web = entra.AddApp("web", o => o.RedirectUris = ["https://localhost/cb"]);
        var api = entra.AddApp("api", o =>
        {
            o.ApplicationType = AuthApplicationType.Api;
            o.IdentifierUris = ["api://test"];
        });

        Assert.Equal("AUTH_ENTRA_WEB", web.Resource.DefaultEnvPrefix);
        Assert.Equal("AUTH_ENTRA_API", api.Resource.DefaultEnvPrefix);
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
        Assert.Equal("plan-auth-web", AuthOpsExtensions.GetPlanAuthStepName("web"));
        Assert.Equal("provision-auth-web", AuthOpsExtensions.GetProvisionAuthStepName("web"));
    }

    [Fact]
    public void Entra_AutoCreatesTenantParameter_WithChoiceInput()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("auth-provider-entra").Entra();

        var tenant = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "auth-provider-entra-tenant-id");
        Assert.Same(tenant, entra.Resource.Resource.TenantIdParameter);
        Assert.Contains(tenant.Annotations.OfType<InputGeneratorAnnotation>(), _ => true);
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

        entra.AddApp("web", o =>
        {
            o.DisplayName = "Test Web";
            o.ApplicationType = AuthApplicationType.Web;
            o.RedirectUris = ["https://localhost/signin-oidc"];
            o.CreateClientSecret = true;
        });

        var steps = await CollectAuthOpsStepsAsync(builder);

        Assert.Single(builder.Resources.OfType<AuthOpsResource>());

        var prereqEntra = Assert.Single(steps, s => s.Name == "prereq-auth-provider-entra-auth");
        Assert.Contains("prereq-providers-auth", prereqEntra.RequiredBySteps);

        var prereqProviders = Assert.Single(steps, s => s.Name == "prereq-providers-auth");
        Assert.Empty(prereqProviders.DependsOnSteps);

        var plan = Assert.Single(steps, s => s.Name == "plan-auth-web");
        Assert.Contains("prereq-providers-auth", plan.DependsOnSteps);
        Assert.DoesNotContain("prereq-auth-provider-entra-auth", plan.DependsOnSteps);

        var provision = Assert.Single(steps, s => s.Name == "provision-auth-web");
        Assert.Contains("plan-auth-web", provision.DependsOnSteps);
        Assert.Contains("deploy-auth", provision.RequiredBySteps);

        var deploy = Assert.Single(steps, s => s.Name == "deploy-auth");
        Assert.Contains(WellKnownPipelineSteps.Deploy, deploy.RequiredBySteps);
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
                     r is AuthOpsResource or AuthOpsResourceBase or AuthAppResource))
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
