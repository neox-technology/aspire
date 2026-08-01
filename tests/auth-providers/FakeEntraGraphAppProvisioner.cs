using Neox.Aspire.Hosting.Auth;

namespace Neox.Aspire.Hosting.Auth.Tests;

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
