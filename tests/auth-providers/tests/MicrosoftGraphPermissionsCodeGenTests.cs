using Neox.Aspire.Hosting.Auth.EntraId.Generators.Internal;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

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
        Assert.Equal("MailReadWrite", MicrosoftGraphPermissionsCodeGen.ToPropertyName("Mail.ReadWrite"));
    }

    [Fact]
    public void Generate_EmitsEnabledPermissionsOnly()
    {
        var catalog = MicrosoftGraphPermissionsCodeGen.ParseCatalog(FixtureCatalog);
        var source = MicrosoftGraphPermissionsCodeGen.Generate(catalog);

        Assert.Contains("public static class MicrosoftGraph", source, StringComparison.Ordinal);
        Assert.Contains("public static class Delegated", source, StringComparison.Ordinal);
        Assert.Contains("public static class Application", source, StringComparison.Ordinal);
        Assert.Contains("public static WellKnownApiPermission UserRead", source, StringComparison.Ordinal);
        Assert.Contains("public static WellKnownApiPermission UserReadAll", source, StringComparison.Ordinal);
        Assert.Contains("e1fe6dd8-ba31-4d61-89e7-88639da4683d", source, StringComparison.Ordinal);
        Assert.Contains("\"Scope\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Role\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MailRead", source, StringComparison.Ordinal);
    }
}
