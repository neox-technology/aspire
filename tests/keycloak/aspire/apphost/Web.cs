using Aspire.Hosting;
using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Web
{
    public static IResourceBuilder<ViteAppResource> Spa { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Spa = builder
            .AddViteApp(ServiceNames.Web.Spa, "../../src/spa")
            .WithExternalHttpEndpoints()
            .WithReference(Services.Api)
            .WithEnvironment("API_BASE_URL", Services.Api.GetEndpoint("http"))
            .WithReference(Auth.Keycloak)
            .WithKeycloakSpa(Auth.SpaClient)
            .WaitFor(Services.Api)
            .WaitFor(Auth.Keycloak);
    }
}
