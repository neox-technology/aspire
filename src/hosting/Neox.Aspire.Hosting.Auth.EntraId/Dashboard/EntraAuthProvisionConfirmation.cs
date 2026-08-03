namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Formats the dashboard confirmation dialog for <c>provision-auth</c>.
/// </summary>
internal static class EntraAuthProvisionConfirmation
{
    public static string BuildTitle(EntraAuthAppRegistrationResource app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return $"Provision app registration — {app.Name}";
    }

    public static string BuildMessage(EntraAuthAppRegistrationResource app, AuthAppRegistrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(plan);

        var modeLabel = plan.Mode switch
        {
            AuthAppRegistrationPlanMode.Create => "Create",
            AuthAppRegistrationPlanMode.Adopt => "Adopt existing",
            _ => plan.Mode.ToString()
        };

        if (plan.IsNoOp)
        {
            return
                $"Provision app registration '{app.Name}' ({modeLabel}).\n\n" +
                "No Graph changes are required.\n\n" +
                "Continue?";
        }

        var lines = new List<string>
        {
            $"Provision app registration '{app.Name}' ({modeLabel}).",
            "",
            "The following tasks will be performed:",
            ""
        };

        foreach (var action in plan.Actions)
        {
            if (action == AuthAppRegistrationPlanAction.None)
            {
                continue;
            }

            lines.Add($"- {GetActionLabel(action)}");
        }

        lines.Add("");
        lines.Add("Continue?");
        return string.Join('\n', lines);
    }

    internal static string GetActionLabel(AuthAppRegistrationPlanAction action) =>
        action switch
        {
            AuthAppRegistrationPlanAction.CreateApplication => "Create application",
            AuthAppRegistrationPlanAction.UpdateDisplayName => "Update display name",
            AuthAppRegistrationPlanAction.UpdateRedirectUris => "Update redirect URIs",
            AuthAppRegistrationPlanAction.UpdateSignInAudience => "Update sign-in audience",
            AuthAppRegistrationPlanAction.UpdateIdentifierUris => "Update identifier URIs",
            AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes => "Update OAuth2 permission scopes",
            AuthAppRegistrationPlanAction.UpdateAppRoles => "Update app roles",
            AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess => "Update API permissions",
            AuthAppRegistrationPlanAction.None => "None",
            _ => action.ToString()
        };
}
