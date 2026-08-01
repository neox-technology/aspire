using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthProviderApiTests
{
    [Fact]
    public void AddAuthProvider_Entra_AddApp_CreatesParametersAndResources()
    {
        var builder = DistributedApplication.CreateBuilder();

        var entra = builder.AddAuthProvider("entra")
            .Entra(o => o.TenantId = "11111111-1111-1111-1111-111111111111");

        var web = entra.AddApp("web", o =>
        {
            o.DisplayName = "Test Web";
            o.ApplicationType = AuthApplicationType.Web;
            o.RedirectUris = ["https://localhost/signin-oidc"];
            o.CreateClientSecret = true;
        });

        Assert.Equal("entra", entra.Resource.Resource.ProviderSlug);
        Assert.Equal("web", web.Resource.Name);
        Assert.Contains(builder.Resources.OfType<AuthOpsResource>(), r => r.Name == AuthOpsResource.DefaultName);
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-tenant-id");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-web-client-id");
        Assert.Contains(builder.Resources.OfType<ParameterResource>(), p => p.Name == "entra-web-client-secret");
    }

    [Fact]
    public void WithAuth_AttachesAnnotationPipeline_WithoutThrowing()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = "t");
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
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = "t");
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
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = "t");
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
        Assert.Equal("prereq-auth", AuthOpsExtensions.AuthPrereqStepName);
        Assert.Equal("prereq-auth-entra", EntraAuthOpsExtensions.AuthPrereqEntraStepName);
        Assert.Equal("deploy-auth", AuthOpsExtensions.AuthDeployStepName);
        Assert.Equal("plan-auth-web", AuthOpsExtensions.GetPlanAuthStepName("web"));
        Assert.Equal("provision-auth-web", AuthOpsExtensions.GetProvisionAuthStepName("web"));
    }
}
