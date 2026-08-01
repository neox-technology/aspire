using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class FakeEntraGraphAppProvisionerTests
{
    [Fact]
    public async Task FakeProvisioner_ReturnsStableIds()
    {
        var provider = new EntraAuthProviderResource("entra") { TenantId = "tenant-1" };
        var app = new AuthAppResource("web", provider)
        {
            Options = new AuthAppOptions
            {
                DisplayName = "Web",
                RedirectUris = ["https://localhost/cb"],
                CreateClientSecret = true
            }
        };
        provider.RegisterApp(app);

        var fake = new FakeEntraGraphAppProvisioner();
        var result = await fake.ProvisionAsync(app, CancellationToken.None);

        Assert.Equal("tenant-1", result.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(result.ClientId));
        Assert.False(string.IsNullOrWhiteSpace(result.ClientSecret));
    }
}

internal sealed class FakeEntraGraphAppProvisioner : IEntraGraphAppProvisioner
{
    public Task<EntraProvisionResult> ProvisionAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        var tenant = app.Provider is EntraAuthProviderResource entra && !string.IsNullOrWhiteSpace(entra.TenantId)
            ? entra.TenantId!
            : "00000000-0000-0000-0000-000000000001";

        return Task.FromResult(new EntraProvisionResult
        {
            TenantId = tenant,
            ClientId = "11111111-1111-1111-1111-111111111111",
            ClientSecret = app.Options.CreateClientSecret ? "fake-secret" : null,
            ApplicationObjectId = "obj-1"
        });
    }
}
