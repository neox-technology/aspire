namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Plans and applies Google Cloud IAM oauth clients.
/// </summary>
public interface IGoogleIamOauthClientProvisioner
{
    Task<GoogleOauthClientPlan> PlanAsync(AuthAppResource app, CancellationToken cancellationToken);

    Task<GoogleProvisionResult> ProvisionAsync(
        AuthAppResource app,
        GoogleOauthClientPlan plan,
        CancellationToken cancellationToken);
}
