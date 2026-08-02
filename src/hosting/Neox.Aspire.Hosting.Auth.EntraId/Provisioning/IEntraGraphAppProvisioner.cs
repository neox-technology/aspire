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

    /// <summary>
    /// Creates a password credential on the Entra application (Graph <c>addPassword</c>)
    /// and returns the one-shot <c>secretText</c>.
    /// </summary>
    Task<string> AddPasswordCredentialAsync(
        string applicationObjectId,
        string displayName,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the Graph application object id for a client (app) id, or null when missing.
    /// </summary>
    Task<string?> TryGetApplicationObjectIdAsync(string clientId, CancellationToken cancellationToken);
}
