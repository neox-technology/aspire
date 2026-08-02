namespace Neox.Aspire.Hosting.Auth.Tests;

internal sealed class FakeGoogleIamOauthClientProvisioner : IGoogleIamOauthClientProvisioner
{
    public GoogleOauthClientPlan? LastPlan { get; private set; }

    public async Task<GoogleOauthClientPlan> PlanAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        string? clientId = null;
        try
        {
            clientId = await app.ClientIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // unset
        }

        if (string.IsNullOrWhiteSpace(clientId)
            || GoogleOauthClientParameterPrompt.IsCreateSentinel(clientId))
        {
            throw new InvalidOperationException(
                "Google AuthOps does not create oauth clients. Set ClientId via the Choice prompt " +
                $"or Parameters__* (Auth app '{app.Name}').");
        }

        var plan = new GoogleOauthClientPlan
        {
            Mode = GoogleOauthClientPlanMode.Bind,
            ProjectId = "project-from-param",
            ClientId = clientId!,
            DesiredDisplayName = app.DisplayName,
            Actions = [GoogleOauthClientPlanAction.BindClientId]
        };
        LastPlan = plan;
        return plan;
    }

    public Task<GoogleProvisionResult> ProvisionAsync(
        AuthAppResource app,
        GoogleOauthClientPlan plan,
        CancellationToken cancellationToken)
    {
        _ = app;
        _ = cancellationToken;
        return Task.FromResult(new GoogleProvisionResult
        {
            ProjectId = plan.ProjectId,
            ClientId = plan.ClientId,
            ClientSecret = null
        });
    }
}
