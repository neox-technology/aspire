namespace Neox.Aspire.Hosting.Auth.Tests;

internal sealed class FakeEntraGraphAppProvisioner : IEntraGraphAppProvisioner
{
    public Task<EntraProvisionResult> ProvisionAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        var tenant = "tenant-from-param";
        return Task.FromResult(new EntraProvisionResult
        {
            TenantId = tenant,
            ClientId = app.Options.ExistingClientId ?? "new-client-id",
            ClientSecret = app.Options.CreateClientSecret ? "secret" : null,
            ApplicationObjectId = "object-id"
        });
    }
}
