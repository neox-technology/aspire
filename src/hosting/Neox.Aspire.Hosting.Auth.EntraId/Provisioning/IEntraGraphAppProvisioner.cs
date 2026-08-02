namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Plans and applies Entra app registrations via Microsoft Graph.
/// </summary>
public interface IEntraGraphAppProvisioner
{
    /// <summary>
    /// Read-only resolve + desired-vs-existing compare (no mutating Graph writes).
    /// </summary>
    Task<AuthAppRegistrationPlan> PlanAsync(AuthAppResource app, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a previously computed <see cref="AuthAppRegistrationPlan"/>.
    /// </summary>
    Task<EntraProvisionResult> ProvisionAsync(
        AuthAppResource app,
        AuthAppRegistrationPlan plan,
        CancellationToken cancellationToken);
}
