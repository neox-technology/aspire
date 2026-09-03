using Azure.Provisioning;
using Xunit;

namespace Neox.Azure.Provisioning.Graph.Tests;

public sealed class GraphApplicationCompileTests
{
    [Fact]
    public void FromExisting_emits_uniqueName_existing_without_arm_name()
    {
        var infrastructure = new Infrastructure();
        var app = GraphApplication.FromExisting("app", "already-registered");
        infrastructure.Add(app);

        var bicep = Compile(infrastructure);

        Assert.Contains("resource app 'Microsoft.Graph/applications@v1.0' existing = {", bicep, StringComparison.Ordinal);
        Assert.Contains("uniqueName:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  name:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("displayName:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_emits_uniqueName_and_web_redirect_uris()
    {
        var infrastructure = new Infrastructure();
        var app = new GraphApplication("app")
        {
            UniqueName = "web",
            DisplayName = "My API",
            SignInAudience = "AzureADMyOrg",
            Web = new GraphWebApplication(),
        };
        infrastructure.Add(app);

        var bicep = Compile(infrastructure);

        Assert.Contains("resource app 'Microsoft.Graph/applications@v1.0' = {", bicep, StringComparison.Ordinal);
        Assert.Contains("uniqueName:", bicep, StringComparison.Ordinal);
        Assert.Contains("displayName:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain(" existing =", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  name:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void Permission_scope_type_emits_admin_or_user()
    {
        var infrastructure = new Infrastructure();
        var app = new GraphApplication("app")
        {
            UniqueName = "web",
            DisplayName = "My API",
            Api = new GraphApiApplication()
        };
        app.Api.RequestedAccessTokenVersion = 2;
        app.Api.Oauth2PermissionScopes.Add(new GraphPermissionScope
        {
            Id = "11111111-1111-4111-8111-111111111111",
            Value = "access_as_user",
            AdminConsentDisplayName = "Access",
            AdminConsentDescription = "Access the API",
            IsEnabled = true,
            Type = "Admin"
        });
        infrastructure.Add(app);

        var bicep = Compile(infrastructure);

        Assert.Contains("oauth2PermissionScopes:", bicep, StringComparison.Ordinal);
        Assert.Contains("requestedAccessTokenVersion: 2", bicep, StringComparison.Ordinal);
        Assert.Contains("value: 'access_as_user'", bicep, StringComparison.Ordinal);
        Assert.Contains("type: 'Admin'", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void Group_and_user_types_are_generated()
    {
        Assert.Equal("Microsoft.Graph/groups", GraphGroup.ResourceTypeName);
        Assert.Equal("Microsoft.Graph/users", GraphUser.ResourceTypeName);
        Assert.Equal("v1.0", GraphGroup.ResourceApiVersion);
    }

    private static string Compile(Infrastructure infrastructure)
    {
        var compilation = infrastructure.Build().Compile();
        return Assert.Single(compilation).Value;
    }
}
