using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak;
using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Services
{
    public static IResourceBuilder<ProjectResource> Api { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Api = builder
            .AddProject<Projects.Neox_Aspire_Hosting_Keycloak_Tests_Api>(ServiceNames.Services.Api)
            .WithExternalHttpEndpoints()
            .WithReference(Auth.Keycloak)
            .WithKeycloakJwtBearer(Auth.ApiClient)
            .WaitFor(Auth.Keycloak);
    }
}
