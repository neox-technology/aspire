using Aspire.Hosting;
using Aspire.Hosting.JavaScript;
using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure.EntraId.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests.AppHost;

public static class Web
{
    public static IResourceBuilder<ViteAppResource> Spa { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Spa = builder
            .AddViteApp(ServiceNames.Web.Spa, "../../src/spa")
            .WithExternalHttpEndpoints()
            .WithReference(Services.Api)
            .WithEntraIdSpaApplication(EntraIdInstance.Workforce, Auth.SpaApp);
    }
}
