using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Keycloak.Tests.AppHost;

public static class Auth
{
    public static IResourceBuilder<KeycloakResource> Keycloak { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder)
    {
        Keycloak = builder.AddKeycloak(ServiceNames.Auth.Keycloak, 8080);
    }
}
