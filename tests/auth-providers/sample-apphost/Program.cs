using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Auth;

var builder = DistributedApplication.CreateBuilder(args);

var entra = builder.AddAuthProvider("entra")
    .Entra();

var blazorAuth = entra.AddApp("blazor-app", o =>
{
    o.DisplayName = "AuthSample-Blazor";
    o.ApplicationType = AuthApplicationType.Web;
    o.RedirectUris = ["https://localhost:7180/signin-oidc"];
    o.CreateClientSecret = true;
});

var opsAuth = entra.AddApp("ops-app", o =>
{
    o.DisplayName = "AuthSample-Ops";
    o.ApplicationType = AuthApplicationType.Spa;
    o.RedirectUris = ["http://localhost:5173"];
    o.CreateClientSecret = false;
});

builder.AddProject<Projects.Neox_Aspire_Hosting_Auth_Tests_SampleBlazor>("blazor")
    .WithExternalHttpEndpoints()
    .WithAuth(blazorAuth);

builder.AddViteApp("ops", "../sample-ops")
    .WithExternalHttpEndpoints()
    .WithAuth(opsAuth, env =>
    {
        env.Map(AuthOutput.TenantId, "VITE_ENTRA_TENANT_ID");
        env.Map(AuthOutput.ClientId, "VITE_ENTRA_CLIENT_ID");
        env.IncludeAuthority = false;
        env.IncludeRedirectUri = true;
        env.Map(AuthOutput.RedirectUri, "VITE_ENTRA_REDIRECT_URI");
    });

builder.Build().Run();
