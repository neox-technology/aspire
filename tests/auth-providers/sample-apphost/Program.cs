using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Auth;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca-env");

// Tenant via Parameters__auth-provider-entra-tenant-id / Choice prompt / Azure__TenantId.
var entra = builder.AddAuthProvider("provider-entra")
    .Entra();

IResourceBuilder<ScopeApiExposition>? accessAsUser = null;

// API Auth app: expose Application ID URI api://{ClientId}, a delegated scope, and an app role.
// Graph User.Read is on the API (GET /me calls Microsoft Graph on behalf of the user).
var apiAuth = entra.AddAppRegistration("appregistration-api", "AuthSample-Api")
    .WithClientSecret()
    .WithApiExposition(api =>
    {
        accessAsUser = api.AddScopeWithAdminAndUserConsent(
            "access_as_user",
            "Access API",
            "Allows the app to access the API as the signed-in user.",
            "Access API",
            "Allow the application to access AuthSample-Api on your behalf.");
    })
    .WithApiPermission(MicrosoftGraph.Delegated.UserRead);

var apiCaller = apiAuth.WithAppRoleExposition(
    AllowedMemberType.Applications,
    "Api.Caller",
    "Applications that call the API");

// Auth app resource names must differ from workload resources (Aspire unique names).
var webAuth = entra.AddAppRegistration("appregistration-web", "AuthSample-Blazor")
    .WithClientSecret()
    .WithLocalhostRedirectUri(AuthApplicationType.Web, path: "signin-oidc")
    .WithApiPermission(accessAsUser!)
    .WithApiPermission(apiCaller);

// SPA is a public client.
var spaAuth = entra.AddAppRegistration("appregistration-spa", "AuthSample-Ops")
    .WithClientSecret()
    .WithLocalhostRedirectUri(AuthApplicationType.Spa, scheme: LocalhostRedirectScheme.Http)
    .WithApiPermission(accessAsUser!);

// HTTP workloads must be Container Apps (not Jobs): Jobs have no ingress, so
// launchSettings / Vite "http" endpoints KeyNotFound during ACA Bicep generation.
var api = builder.AddProject<Projects.Neox_Aspire_Hosting_Auth_Tests_SampleApi>("api")
    .WithExternalHttpEndpoints()
    .WithAuth(apiAuth)
    .PublishAsAzureContainerApp((_, _) => { });

var apiScope = ReferenceExpression.Create(
    $"api://{apiAuth.Resource.ClientIdParameter}/access_as_user");

builder.AddProject<Projects.Neox_Aspire_Hosting_Auth_Tests_SampleBlazor>("blazor")
    .WithExternalHttpEndpoints()
    .WithAuth(webAuth)
    .WithReference(api)
    .WithEnvironment("DownstreamApi__Scopes__0", apiScope)
    .WithEnvironment("DownstreamApi__BaseUrl", api.GetEndpoint("https"))
    .PublishAsAzureContainerApp((_, _) => { });

builder.AddViteApp("ops", "../sample-ops")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithAuth(spaAuth, env =>
    {
        env.IncludeInstance = false;
        env.Map(AuthOutput.TenantId, "VITE_ENTRA_TENANT_ID");
        env.Map(AuthOutput.ClientId, "VITE_ENTRA_CLIENT_ID");
    })
    .WithEnvironment("VITE_API_SCOPE", apiScope)
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .PublishAsAzureContainerApp((_, _) => { });

builder.Build().Run();
