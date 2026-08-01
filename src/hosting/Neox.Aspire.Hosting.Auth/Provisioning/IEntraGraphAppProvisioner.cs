namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Creates or adopts Entra app registrations via Microsoft Graph.
/// </summary>
public interface IEntraGraphAppProvisioner
{
    Task<EntraProvisionResult> ProvisionAsync(AuthAppResource app, CancellationToken cancellationToken);
}
