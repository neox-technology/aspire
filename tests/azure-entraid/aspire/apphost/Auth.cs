using System.Security.Cryptography.X509Certificates;
using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure.EntraId.Tests.ServiceDefaults;

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests.AppHost;

public static class Auth {
    public static IResourceBuilder<AsymmetricX509CertResource> ApiCert { get; private set; } = null!;
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> Api { get; private set; } = null!;
    public static IResourceBuilder<AzureEntraIdWebApplicationResource> ApiSwagger { get; private set; } = null!;
    public static IResourceBuilder<AzureEntraIdAppRegistrationResource> Spa { get; private set; } = null!;
    public static IResourceBuilder<AzureEntraIdSpaApplicationResource> SpaApp { get; private set; } = null!;

    public static void Configure(IDistributedApplicationBuilder builder) {
        var thumbprint = builder.AddParameter($"{ServiceNames.Auth.ApiCert}-thumbprint", secret: true);
        ApiCert = builder.AddCertificate(ServiceNames.Auth.ApiCert, thumbprint, StoreLocation.CurrentUser);

        Api = builder.AddAzureAppRegistration(ServiceNames.Auth.Api)
            .WithDefaultIdentifierUri()
            .WithKeyCredential(ApiCert);

        ApiSwagger = Api.AddWebApplication(ServiceNames.Auth.ApiSwagger)
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));

        var accessAsUserScope = Api.AddScope(ServiceNames.Auth.AccessAsUserScope, "access_as_user", "Allow to access to user.", "Allow to access to user.", "Allow to access to user.", "Allow to access to user.");
        Api.AddAppRole("daemon", AllowedMemberTypes.Application, "Daemon.Access", "Daemon access", "App-only access");

        Spa = builder.AddAzureAppRegistration(ServiceNames.Auth.Spa)
            .WithDefaultIdentifierUri()
            .WithPermission(accessAsUserScope);

        SpaApp = Spa.AddSpaApplication(ServiceNames.Auth.SpaApp)
            .WithRedirectUri(new Uri("http://localhost/"));
    }
}
