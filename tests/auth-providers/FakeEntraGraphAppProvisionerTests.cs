using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class FakeEntraGraphAppProvisionerTests
{
    [Fact]
    public async Task FakeProvisioner_ReturnsStableIds()
    {
        var provider = new EntraAuthOpsResource("entra")
        {
            TenantIdParameter = CreateParameter("tenant", "tenant-1")
        };
        var app = new AuthAppResource("web", provider)
        {
            Options = new AuthAppOptions
            {
                DisplayName = "Web",
                RedirectUris = ["https://localhost/cb"],
                CreateClientSecret = true
            },
            TenantIdParameter = provider.TenantIdParameter
        };
        provider.RegisterApp(app);

        var fake = new FakeEntraGraphAppProvisioner();
        var result = await fake.ProvisionAsync(app, CancellationToken.None);

        Assert.Equal("tenant-from-param", result.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(result.ClientId));
        Assert.False(string.IsNullOrWhiteSpace(result.ClientSecret));
    }

    [Fact]
    public async Task FakeProvisioner_SpaWithoutSecret_ReturnsNullClientSecret()
    {
        var provider = new EntraAuthOpsResource("entra")
        {
            TenantIdParameter = CreateParameter("tenant", "tenant-1")
        };
        var app = new AuthAppResource("spa", provider)
        {
            Options = new AuthAppOptions
            {
                DisplayName = "Spa",
                ApplicationType = AuthApplicationType.Spa,
                RedirectUris = ["http://localhost:5173"],
                CreateClientSecret = false
            },
            TenantIdParameter = provider.TenantIdParameter
        };
        provider.RegisterApp(app);

        var result = await new FakeEntraGraphAppProvisioner().ProvisionAsync(app, CancellationToken.None);

        Assert.Null(result.ClientSecret);
    }

    private static ParameterResource CreateParameter(string name, string value)
    {
        return new ParameterResource(name, _ => value, secret: false);
    }
}
