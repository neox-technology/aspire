using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Auth;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca-env");

// Tenant via Parameters__auth-provider-entra-tenant-id / Choice prompt / Azure__TenantId.
var entra = builder.AddAuthProvider("provider-entra")
    .Entra();

var google = builder.AddAuthProvider("provider-google")
    .Google();

IResourceBuilder<ScopeApiExposition>? accessAsUser = null;

// API Auth app: expose Application ID URI api://{ClientId}, a delegated scope, and an app role.
var apiAuth = entra.AddAppRegistration("appregistration-api", "AuthSample-Api")
    .WithApiExposition(api =>
    {
        accessAsUser = api.AddScopeWithAdminAndUserConsent(
            "access_as_user",
            "Access API",
            "Allows the app to access the API as the signed-in user.",
            "Access API",
            "Allow the application to access AuthSample-Api on your behalf.");
    });

var apiCaller = apiAuth.WithAppRoleExposition(
    AllowedMemberType.Applications,
    "Api.Caller",
    "Applications that call the API");

// Auth app resource names must differ from workload resources (Aspire unique names).
var webAuth = entra.AddAppRegistration("appregistration-web", "AuthSample-Blazor")
    .WithLocalhostRedirectUri(AuthApplicationType.Web, path: "signin-oidc")
    .WithApiPermission(accessAsUser!)
    .WithApiPermission(apiCaller)
    .WithApiPermission(MicrosoftGraph.Delegated.UserRead);

var spaAuth = entra.AddAppRegistration("appregistration-spa", "AuthSample-Ops")
    .WithLocalhostRedirectUri(AuthApplicationType.Spa)
    .WithApiPermission(accessAsUser!);

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

// Google adopt/bind — ClientId via Choice / Parameters__*; redirects are model desired-state only.
var googleWebAuth = google.AddAppRegistration("appregistration-google-web", "AuthSample-Google-Web")
    .WithLocalhostRedirectUri(7281, "/signin-oidc");

builder.AddProject<Projects.Neox_Aspire_Hosting_Auth_Tests_SampleBlazor>("blazor-google")
    .WithExternalHttpEndpoints()
    .WithAuth(googleWebAuth)
    .PublishAsAzureContainerApp((_, _) => { });

builder.Build().Run();
