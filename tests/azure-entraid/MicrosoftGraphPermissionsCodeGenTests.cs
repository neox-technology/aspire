using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure.EntraId.Generators.Internal;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests;

public sealed class MicrosoftGraphPermissionsCodeGenTests
{
    private const string FixtureCatalog = """
        {
          "appId": "00000003-0000-0000-c000-000000000000",
          "delegated": [
            {
              "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
              "value": "User.Read",
              "displayName": "Sign in and read user profile",
              "description": "Allows users to sign-in to the app.",
              "isEnabled": true
            },
            {
              "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "value": "Mail.Read",
              "displayName": "Read user mail",
              "description": "Read mail.",
              "isEnabled": false
            }
          ],
          "application": [
            {
              "id": "df021288-bdef-4463-88db-98f22de89214",
              "value": "User.Read.All",
              "displayName": "Read all users full profiles",
              "description": "Read all users.",
              "isEnabled": true
            }
          ]
        }
        """;

    [Fact]
    public void Generate_EmitsEnabledPermissionsOnly()
    {
        var catalog = MicrosoftGraphPermissionsCodeGen.ParseCatalog(FixtureCatalog);
        var source = MicrosoftGraphPermissionsCodeGen.Generate(catalog);

        Assert.Contains("namespace Neox.Aspire.Hosting.Azure;", source, StringComparison.Ordinal);
        Assert.Contains("public static class MicrosoftGraph", source, StringComparison.Ordinal);
        Assert.Contains("public static class Delegated", source, StringComparison.Ordinal);
        Assert.Contains("public static class Application", source, StringComparison.Ordinal);
        Assert.Contains("public static AzureEntraIdWellKnownPermission UserRead", source, StringComparison.Ordinal);
        Assert.Contains("public static AzureEntraIdWellKnownPermission UserReadAll", source, StringComparison.Ordinal);
        Assert.Contains("e1fe6dd8-ba31-4d61-89e7-88639da4683d", source, StringComparison.Ordinal);
        Assert.Contains("AzureEntraIdPermissionType.Scope", source, StringComparison.Ordinal);
        Assert.Contains("AzureEntraIdPermissionType.Role", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MailRead", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MicrosoftGraph_Delegated_UnissonPermissions()
    {
        Assert.Equal(MicrosoftGraphPermissionsCodeGen.MicrosoftGraphAppId, MicrosoftGraph.AppId);
        Assert.Equal(AzureEntraIdPermissionType.Scope, MicrosoftGraph.Delegated.UserReadAll.Type);
        Assert.Equal(AzureEntraIdPermissionType.Scope, MicrosoftGraph.Delegated.ApplicationReadAll.Type);
        Assert.Equal(AzureEntraIdPermissionType.Scope, MicrosoftGraph.Delegated.AppRoleAssignmentReadWriteAll.Type);
        Assert.Equal(MicrosoftGraph.AppId, MicrosoftGraph.Delegated.UserReadAll.ResourceAppId);
        Assert.Equal(Guid.Parse("a154be20-db9c-4678-8ab7-66f6cc099a59"), MicrosoftGraph.Delegated.UserReadAll.Id);
        Assert.Equal("User.Read.All", MicrosoftGraph.Delegated.UserReadAll.Value);
        Assert.Equal(Guid.Parse("c79f8feb-a9db-4090-85f9-90d820caa0eb"), MicrosoftGraph.Delegated.ApplicationReadAll.Id);
        Assert.Equal("Application.Read.All", MicrosoftGraph.Delegated.ApplicationReadAll.Value);
        Assert.Equal(Guid.Parse("84bccea3-f856-4a8a-967b-dbe0a3d53a64"), MicrosoftGraph.Delegated.AppRoleAssignmentReadWriteAll.Id);
        Assert.Equal("AppRoleAssignment.ReadWrite.All", MicrosoftGraph.Delegated.AppRoleAssignmentReadWriteAll.Value);
    }

    [Fact]
    public void ParseCatalog_ReadsDelegatedAndApplication()
    {
        var catalog = MicrosoftGraphPermissionsCodeGen.ParseCatalog(FixtureCatalog);

        Assert.Equal(MicrosoftGraphPermissionsCodeGen.MicrosoftGraphAppId, catalog.AppId);
        Assert.Equal(2, catalog.Delegated.Count);
        Assert.Equal("User.Read", catalog.Delegated[0].Value);
        Assert.False(catalog.Delegated[1].IsEnabled);
        Assert.Equal("User.Read.All", Assert.Single(catalog.Application).Value);
    }

    [Fact]
    public void ToPropertyName_SanitizesPermissionValues()
    {
        Assert.Equal("UserRead", MicrosoftGraphPermissionsCodeGen.ToPropertyName("User.Read"));
        Assert.Equal("UserReadAll", MicrosoftGraphPermissionsCodeGen.ToPropertyName("User.Read.All"));
        Assert.Equal("ApplicationReadAll", MicrosoftGraphPermissionsCodeGen.ToPropertyName("Application.Read.All"));
        Assert.Equal("AppRoleAssignmentReadWriteAll", MicrosoftGraphPermissionsCodeGen.ToPropertyName("AppRoleAssignment.ReadWrite.All"));
        Assert.Equal("MailReadWrite", MicrosoftGraphPermissionsCodeGen.ToPropertyName("Mail.ReadWrite"));
    }
}
