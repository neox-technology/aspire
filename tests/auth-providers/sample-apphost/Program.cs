using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Auth;

var builder = DistributedApplication.CreateBuilder(args);

// Tenant via Parameters__auth-provider-entra-tenant-id / Choice prompt / Azure__TenantId.
var entra = builder.AddAuthProvider("provider-entra")
    .Entra();

// Auth app resource names must differ from workload resources (Aspire unique names).
var webAuth = entra.AddAppRegistration("appregistration-web", "AuthSample-Blazor");
var spaAuth = entra.AddAppRegistration("appregistration-spa", "AuthSample-Ops");

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
    });

builder.Build().Run();
