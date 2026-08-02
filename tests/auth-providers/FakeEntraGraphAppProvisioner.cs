namespace Neox.Aspire.Hosting.Auth.Tests;

internal sealed class FakeEntraGraphAppProvisioner : IEntraGraphAppProvisioner
{
    public AuthAppRegistrationPlan? LastPlan { get; private set; }

    public Task<AuthAppRegistrationPlan> PlanAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        var plan = new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Create,
            TenantId = "tenant-from-param",
            DesiredDisplayName = app.DisplayName,
            DesiredSignInAudience = SupportedAccountsMapping.GetDesiredSignInAudience(app),
            Existing = null,
            Actions = [AuthAppRegistrationPlanAction.CreateApplication]
        };
        LastPlan = plan;
        return Task.FromResult(plan);
    }

    public Task<EntraProvisionResult> ProvisionAsync(
        AuthAppResource app,
        AuthAppRegistrationPlan plan,
        CancellationToken cancellationToken)
    {
        _ = app;
        return Task.FromResult(new EntraProvisionResult
        {
            TenantId = plan.TenantId,
            ClientId = plan.Existing?.AppId ?? "new-client-id",
            ClientSecret = null,
            ApplicationObjectId = plan.Existing?.ObjectId ?? "object-id"
        });
    }
}
