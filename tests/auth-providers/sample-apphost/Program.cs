using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Auth;

var builder = DistributedApplication.CreateBuilder(args);

// Tenant via Parameters__entra-tenant-id / Azure__TenantId / interactive prompt.
var entra = builder.AddAuthProvider("auth-provider-entra")
    .Entra();

// Auth app resource names must differ from workload resources (Aspire unique names).
var webAuth = entra.AddApp("auth-appregistration-web", o =>
{
    o.DisplayName = "AuthSample-Blazor";
    o.ApplicationType = AuthApplicationType.Web;
    o.RedirectUris = ["https://localhost:7180/signin-oidc"];
    o.CreateClientSecret = true;
});

var spaAuth = entra.AddApp("auth-appregistration-spa", o =>
{
    o.DisplayName = "AuthSample-Ops";
    o.ApplicationType = AuthApplicationType.Spa;
    o.RedirectUris = ["http://localhost:5173"];
    o.CreateClientSecret = false;
});

builder.AddProject<Projects.Neox_Aspire_Hosting_Auth_Tests_SampleBlazor>("blazor")
    .WithExternalHttpEndpoints()
    .WithAuth(webAuth);

builder.AddViteApp("ops", "../sample-ops")
    .WithExternalHttpEndpoints()
    .WithAuth(spaAuth, env =>
    {
        env.Map(AuthOutput.TenantId, "VITE_ENTRA_TENANT_ID");
        env.Map(AuthOutput.ClientId, "VITE_ENTRA_CLIENT_ID");
        env.IncludeAuthority = false;
        env.IncludeRedirectUri = true;
        env.Map(AuthOutput.RedirectUri, "VITE_ENTRA_REDIRECT_URI");
    });

builder.Build().Run();
