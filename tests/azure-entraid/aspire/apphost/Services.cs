using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure.EntraId.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests.AppHost;

public static class Services
{
    public static IResourceBuilder<ProjectResource> Api { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Api = builder
            .AddProject<Projects.Neox_Aspire_Hosting_Azure_EntraId_Tests_Api>(ServiceNames.Services.Api)
            .WithExternalHttpEndpoints()
            .WithMicrosoftIdentityWebApplication(EntraIdInstance.Workforce, Auth.ApiSwagger);
    }
}
