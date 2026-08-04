using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class EntraAuthProvisionConfirmationTests
{
    [Fact]
    public void BuildTitle_IncludesAppName()
    {
        var app = CreateApp("web", "Web App");

        var title = EntraAuthProvisionConfirmation.BuildTitle(app);

        Assert.Equal("Provision app registration — web", title);
    }

    [Fact]
    public void BuildMessage_CreatePlan_ListsPlannedActions()
    {
        var app = CreateApp("web", "Web App");
        var plan = new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Create,
            TenantId = "tenant-1",
            DesiredDisplayName = "Web App",
            Actions =
            [
                AuthAppRegistrationPlanAction.CreateApplication,
                AuthAppRegistrationPlanAction.UpdateIdentifierUris,
                AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes,
                AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess
            ]
        };

        var message = EntraAuthProvisionConfirmation.BuildMessage(app, plan);

        Assert.Contains("Provision app registration 'web' (Create).", message);
        Assert.Contains("The following tasks will be performed:", message);
        Assert.Contains("- Create application", message);
        Assert.Contains("- Update identifier URIs", message);
        Assert.Contains("- Update OAuth2 permission scopes", message);
        Assert.Contains("- Update API permissions", message);
        Assert.Contains("Continue?", message);
        Assert.DoesNotContain("No Graph changes are required.", message);
    }

    [Fact]
    public void BuildMessage_AdoptPlan_SingleAction()
    {
        var app = CreateApp("spa", "Spa");
        var plan = new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Adopt,
            TenantId = "tenant-1",
            DesiredDisplayName = "Spa",
            Existing = new AuthAppRegistrationExistingSnapshot
            {
                ObjectId = "obj-1",
                AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                DisplayName = "Spa"
            },
            Actions = [AuthAppRegistrationPlanAction.UpdateRedirectUris]
        };

        var message = EntraAuthProvisionConfirmation.BuildMessage(app, plan);

        Assert.Contains("Provision app registration 'spa' (Adopt existing).", message);
        Assert.Contains("- Update redirect URIs", message);
        Assert.DoesNotContain("- Create application", message);
    }

    [Fact]
    public void BuildMessage_NoOpPlan_SaysNoChangesRequired()
    {
        var app = CreateApp("spa", "Spa");
        var plan = new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Adopt,
            TenantId = "tenant-1",
            DesiredDisplayName = "Spa",
            Existing = new AuthAppRegistrationExistingSnapshot
            {
                ObjectId = "obj-1",
                AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                DisplayName = "Spa"
            },
            Actions = [AuthAppRegistrationPlanAction.None]
        };

        var message = EntraAuthProvisionConfirmation.BuildMessage(app, plan);

        Assert.Contains("Provision app registration 'spa' (Adopt existing).", message);
        Assert.Contains("No Graph changes are required.", message);
        Assert.Contains("Continue?", message);
        Assert.DoesNotContain("The following tasks will be performed:", message);
    }

    [Theory]
    [InlineData(AuthAppRegistrationPlanAction.CreateApplication, "Create application")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateDisplayName, "Update display name")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateRedirectUris, "Update redirect URIs")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateSignInAudience, "Update sign-in audience")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateIdentifierUris, "Update identifier URIs")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes, "Update OAuth2 permission scopes")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateAppRoles, "Update app roles")]
    [InlineData(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess, "Update API permissions")]
    public void GetActionLabel_ReturnsExpectedLabel(AuthAppRegistrationPlanAction action, string expected)
    {
        Assert.Equal(expected, EntraAuthProvisionConfirmation.GetActionLabel(action));
    }

    private static EntraAuthAppRegistrationResource CreateApp(string name, string displayName)
    {
        var provider = new EntraAuthOpsResource("entra", new AuthOpsResource("auth-ops"));
        return new EntraAuthAppRegistrationResource(name, provider, displayName);
    }
}
