namespace Neox.Aspire.Hosting.Auth.Tests;

internal sealed class FakeEntraGraphAppProvisioner : IEntraGraphAppProvisioner
{
    public AuthAppRegistrationPlan? LastPlan { get; private set; }

    public Task<AuthAppRegistrationPlan> PlanAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        var desiredIdentifierUris = EntraApiExpositionApplicator.CollectDesiredIdentifierUris(app);
        var desiredScopes = EntraApiExpositionApplicator.CollectDesiredScopes(app);
        var desiredAppRoles = EntraApiExpositionApplicator.CollectDesiredAppRoles(app);
        var desiredPermissions = EntraApiPermissionApplicator.CollectDesired(app, _ => null);

        var actions = new List<AuthAppRegistrationPlanAction>
        {
            AuthAppRegistrationPlanAction.CreateApplication
        };
        if (desiredIdentifierUris.Count > 0)
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateIdentifierUris);
        }

        if (desiredScopes.Count > 0)
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes);
        }

        if (desiredAppRoles.Count > 0)
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateAppRoles);
        }

        if (EntraApiPermissionApplicator.HasDeclaredPermissions(app))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess);
        }

        var plan = new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Create,
            TenantId = "tenant-from-param",
            DesiredDisplayName = app.DisplayName,
            DesiredSignInAudience = SupportedAccountsMapping.GetDesiredSignInAudience(app),
            DesiredIdentifierUris = desiredIdentifierUris,
            DesiredScopes = desiredScopes,
            DesiredAppRoles = desiredAppRoles,
            DesiredRequiredResourceAccess = desiredPermissions,
            Existing = null,
            Actions = actions
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
