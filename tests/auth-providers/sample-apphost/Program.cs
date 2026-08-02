using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Auth;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca-env");

// Tenant via Parameters__auth-provider-entra-tenant-id / Choice prompt / Azure__TenantId.
var entra = builder.AddAuthProvider("provider-entra")
    .Entra();

// Auth app resource names must differ from workload resources (Aspire unique names).
var webAuth = entra.AddAppRegistration("appregistration-web", "AuthSample-Blazor")
    .WithLocalhostRedirectUri(AuthApplicationType.Web, path: "signin-oidc");

var spaAuth = entra.AddAppRegistration("appregistration-spa", "AuthSample-Ops")
    .WithLocalhostRedirectUri(AuthApplicationType.Spa);

// HTTP workloads must be Container Apps (not Jobs): Jobs have no ingress, so
// launchSettings / Vite "http" endpoints KeyNotFound during ACA Bicep generation.
builder.AddProject<Projects.Neox_Aspire_Hosting_Auth_Tests_SampleBlazor>("blazor")
    .WithExternalHttpEndpoints()
    .WithAuth(webAuth)
    .PublishAsAzureContainerApp((_, _) => { });

builder.AddViteApp("ops", "../sample-ops")
    .WithExternalHttpEndpoints()
    .WithAuth(spaAuth, env =>
    {
        env.Map(AuthOutput.TenantId, "VITE_ENTRA_TENANT_ID");
        env.Map(AuthOutput.ClientId, "VITE_ENTRA_CLIENT_ID");
        env.IncludeAuthority = false;
    })
    .PublishAsAzureContainerApp((_, _) => { });

builder.Build().Run();
