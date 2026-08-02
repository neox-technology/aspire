namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Plans and applies Entra app registrations via Microsoft Graph.
/// </summary>
public interface IEntraGraphAppProvisioner
{
    /// <summary>
    /// Read-only resolve + desired-vs-existing compare (no mutating Graph writes).
    /// </summary>
    Task<AuthAppRegistrationPlan> PlanAsync(EntraAuthAppRegistrationResource app, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a previously computed <see cref="AuthAppRegistrationPlan"/>.
    /// </summary>
    Task<EntraProvisionResult> ProvisionAsync(
        EntraAuthAppRegistrationResource app,
        AuthAppRegistrationPlan plan,
        CancellationToken cancellationToken);
}
