namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Plans and applies Google Cloud IAM oauth clients.
/// </summary>
public interface IGoogleIamOauthClientProvisioner
{
    Task<GoogleOauthClientPlan> PlanAsync(GoogleAuthAppRegistrationResource app, CancellationToken cancellationToken);

    Task<GoogleProvisionResult> ProvisionAsync(
        GoogleAuthAppRegistrationResource app,
        GoogleOauthClientPlan plan,
        CancellationToken cancellationToken);
}
