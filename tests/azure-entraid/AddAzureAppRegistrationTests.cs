using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure;
using Neox.Azure.Provisioning.Graph;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests;

public sealed class AddAzureAppRegistrationTests
{
    [Fact]
    public void AddAzureAppRegistration_registers_bicep_resource_with_default_parameters()
    {
        var builder = DistributedApplication.CreateBuilder();

        var app = builder.AddAzureAppRegistration("web");

        Assert.Equal("web", app.Resource.Name);
        Assert.IsType<AzureEntraIdAppRegistrationResource>(app.Resource);
        Assert.IsAssignableFrom<AzureProvisioningResource>(app.Resource);
        Assert.False(app.Resource.IsExisting());
        Assert.Equal("web", app.Resource.Parameters[AzureEntraIdAppRegistrationResource.UniqueNameParameter]);
        Assert.Equal("web", app.Resource.Parameters[AzureEntraIdAppRegistrationResource.DisplayNameParameter]);
    }

    [Fact]
    public void GetBicepTemplateString_create_declares_graph_application_and_outputs()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");

        var bicep = app.Resource.GetBicepTemplateString();

        Assert.Contains("extension microsoftGraphV1", bicep, StringComparison.Ordinal);
        Assert.Contains("resource app 'Microsoft.Graph/applications@v1.0' = {", bicep, StringComparison.Ordinal);
        Assert.Contains("uniqueName:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  name:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain(" existing =", bicep, StringComparison.Ordinal);
        Assert.Contains("resource sp 'Microsoft.Graph/servicePrincipals@v1.0' = {", bicep, StringComparison.Ordinal);
        Assert.Contains("output clientId string = app.appId", bicep, StringComparison.Ordinal);
        Assert.Contains("output objectId string = app.id", bicep, StringComparison.Ordinal);
        Assert.Contains("param displayName string", bicep, StringComparison.Ordinal);
        Assert.Contains("redirectUris: redirectUris", bicep, StringComparison.Ordinal);
        Assert.Contains("signInAudience: 'AzureADMyOrg'", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_emits_existing_keyword_without_displayName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var uniqueName = builder.AddParameter("existingUniqueName");
        var app = builder.AddAzureAppRegistration("web")
            .AsExisting(uniqueName, resourceGroupParameter: null);

        Assert.True(app.Resource.IsExisting());

        var bicep = app.Resource.GetBicepTemplateString();

        Assert.Contains("resource app 'Microsoft.Graph/applications@v1.0' existing = {", bicep, StringComparison.Ordinal);
        Assert.Contains("uniqueName:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  name:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("displayName:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("signInAudience:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("param displayName", bicep, StringComparison.Ordinal);
        Assert.Contains("output clientId string = app.appId", bicep, StringComparison.Ordinal);
        Assert.Same(uniqueName.Resource, app.Resource.Parameters[AzureEntraIdAppRegistrationResource.UniqueNameParameter]);
    }

    [Fact]
    public void RunAsExisting_uses_string_unique_name()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .RunAsExisting("already-registered", resourceGroup: null!);

        Assert.True(app.Resource.IsExisting());
        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains(" existing =", bicep, StringComparison.Ordinal);
        Assert.Equal("already-registered", app.Resource.Parameters[AzureEntraIdAppRegistrationResource.UniqueNameParameter]);
    }

    [Fact]
    public void WithDisplayName_and_WithRedirectUri_set_parameters()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithDisplayName("My API")
            .WithRedirectUri("https://localhost:7281/signin-oidc")
            .WithRedirectUri("https://localhost:7281/signout-oidc");

        Assert.Equal("My API", app.Resource.Parameters[AzureEntraIdAppRegistrationResource.DisplayNameParameter]);
        var uris = Assert.IsType<string[]>(app.Resource.Parameters[AzureEntraIdAppRegistrationResource.RedirectUrisParameter]);
        Assert.Equal(["https://localhost:7281/signin-oidc", "https://localhost:7281/signout-oidc"], uris);
    }

    [Fact]
    public void WithSupportedAccountType_emits_sign_in_audience()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithSupportedAccountType(SupportedAccountType.AzureADMultipleOrgs);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains("signInAudience: 'AzureADMultipleOrgs'", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("signInAudience: 'AzureADMyOrg'", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("requestedAccessTokenVersion:", bicep, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SupportedAccountType.AzureADandPersonalMicrosoftAccount, "AzureADandPersonalMicrosoftAccount")]
    [InlineData(SupportedAccountType.PersonalMicrosoftAccount, "PersonalMicrosoftAccount")]
    public void WithSupportedAccountType_msa_audience_emits_access_token_version_2(
        SupportedAccountType accountType,
        string signInAudience)
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithSupportedAccountType(accountType);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains($"signInAudience: '{signInAudience}'", bicep, StringComparison.Ordinal);
        Assert.Contains("requestedAccessTokenVersion: 2", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("requestedAccessTokenVersion: 0", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_signInAudience()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithSupportedAccountType(SupportedAccountType.AzureADandPersonalMicrosoftAccount)
            .RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("signInAudience:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("requestedAccessTokenVersion:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void WithDefaultIdentifierUri_msa_audience_repeats_sign_in_audience_on_upsert()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithSupportedAccountType(SupportedAccountType.AzureADandPersonalMicrosoftAccount)
            .WithDefaultIdentifierUri();

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains("resource appIdentifierUris 'Microsoft.Graph/applications@v1.0' = {", bicep, StringComparison.Ordinal);
        Assert.Contains("signInAudience: 'AzureADandPersonalMicrosoftAccount'", bicep, StringComparison.Ordinal);
        Assert.Contains("requestedAccessTokenVersion: 2", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("requestedAccessTokenVersion: 0", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void WithIdentifierUri_sets_parameter()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithIdentifierUri(new Uri("https://api.contoso.com"));

        var uris = Assert.IsType<string[]>(app.Resource.Parameters[AzureEntraIdAppRegistrationResource.IdentifierUrisParameter]);
        Assert.Equal(["https://api.contoso.com"], uris);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains("identifierUris: identifierUris", bicep, StringComparison.Ordinal);
        Assert.Contains("param identifierUris array", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void WithDefaultIdentifierUri_emits_api_appId_interpolation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithDefaultIdentifierUri();

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains("resource appIdentifierUris 'Microsoft.Graph/applications@v1.0' = {", bicep, StringComparison.Ordinal);
        Assert.Contains("identifierUris:", bicep, StringComparison.Ordinal);
        Assert.Contains("api://${app.appId}", bicep, StringComparison.Ordinal);
        Assert.Contains("requestedAccessTokenVersion: 2", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("requestedAccessTokenVersion: 0", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_identifierUris()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .WithIdentifierUri(new Uri("https://api.contoso.com"))
            .WithDefaultIdentifierUri()
            .RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("identifierUris:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("param identifierUris", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateFile_writes_bicepconfig_beside_module()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var dir = Directory.CreateTempSubdirectory("entraid-bicep-test").FullName;

        try
        {
            using var template = app.Resource.GetBicepTemplateFile(dir, deleteTemporaryFileOnDispose: false);
            var configPath = Path.Combine(dir, "bicepconfig.json");

            Assert.True(File.Exists(template.Path));
            Assert.True(File.Exists(configPath));
            var config = File.ReadAllText(configPath);
            Assert.Contains("microsoftgraph/v1.0:1.0.0", config, StringComparison.Ordinal);
            Assert.Contains("extensibility", config, StringComparison.Ordinal);
            Assert.Equal(File.ReadAllText(template.Path), app.Resource.GetBicepTemplateString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ConfigureInfrastructure_can_mutate_sign_in_audience()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .ConfigureInfrastructure(infra =>
            {
                var graphApp = infra.GetProvisionableResources().OfType<GraphApplication>().Single();
                graphApp.SignInAudience = "AzureADMultipleOrgs";
            });

        var bicep = app.Resource.GetBicepTemplateString();

        Assert.Contains("signInAudience: 'AzureADMultipleOrgs'", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void AddScope_registers_child_with_app_registration_parent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var scope = app.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");

        Assert.IsType<AzureEntraIdScopeResource>(scope.Resource);
        Assert.Same(app.Resource, scope.Resource.Parent);
        Assert.Equal("access_as_user", scope.Resource.Value);
        Assert.Equal(AzureEntraIdScopeResource.AdminConsentType, scope.Resource.ConsentType);
        Assert.Null(scope.Resource.UserConsentDisplayName);
        Assert.Single(app.Resource.Scopes, scope.Resource);
    }

    [Fact]
    public void AddScope_with_user_consent_sets_user_type()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var scope = app.AddScope(
            "access",
            "access_as_user",
            "Access API",
            "Allows the app to access the API as admin",
            "Access API",
            "Allows the app to access the API on your behalf");

        Assert.Equal(AzureEntraIdScopeResource.UserConsentType, scope.Resource.ConsentType);
        Assert.Equal("Access API", scope.Resource.UserConsentDisplayName);
        Assert.Equal("Allows the app to access the API on your behalf", scope.Resource.UserConsentDescription);
    }

    [Fact]
    public void AddScope_duplicate_value_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        app.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");

        var ex = Assert.Throws<ArgumentException>(() =>
            app.AddScope("access-again", "access_as_user", "Access API", "Allows the app to access the API"));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void GetBicepTemplateString_create_emits_oauth2_permission_scopes()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var admin = app.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        app.AddScope(
            "read",
            "api.read",
            "Read API",
            "Allows the app to read the API as admin",
            "Read API",
            "Allows the app to read the API on your behalf");

        var bicep = app.Resource.GetBicepTemplateString();
        var expectedId = admin.Resource.Id.ToString("D");

        Assert.Contains("oauth2PermissionScopes:", bicep, StringComparison.Ordinal);
        Assert.Contains("value: 'access_as_user'", bicep, StringComparison.Ordinal);
        Assert.Contains("adminConsentDisplayName: 'Access API'", bicep, StringComparison.Ordinal);
        Assert.Contains("adminConsentDescription: 'Allows the app to access the API'", bicep, StringComparison.Ordinal);
        Assert.Contains("type: 'Admin'", bicep, StringComparison.Ordinal);
        Assert.Contains($"id: '{expectedId}'", bicep, StringComparison.Ordinal);
        Assert.Contains("isEnabled: true", bicep, StringComparison.Ordinal);
        Assert.Contains("value: 'api.read'", bicep, StringComparison.Ordinal);
        Assert.Contains("type: 'User'", bicep, StringComparison.Ordinal);
        Assert.Contains("userConsentDisplayName: 'Read API'", bicep, StringComparison.Ordinal);
        Assert.Contains("userConsentDescription: 'Allows the app to read the API on your behalf'", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("userConsentDisplayName: 'Access API'", bicep, StringComparison.Ordinal);
        Assert.Contains("requestedAccessTokenVersion: 2", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("requestedAccessTokenVersion: 0", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_oauth2_permission_scopes()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .RunAsExisting("already-registered", resourceGroup: null!);
        app.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("oauth2PermissionScopes:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("access_as_user", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void AddScope_graph_id_is_stable_across_builders()
    {
        var first = DistributedApplication.CreateBuilder();
        var firstScope = first.AddAzureAppRegistration("web")
            .AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");

        var second = DistributedApplication.CreateBuilder();
        var secondScope = second.AddAzureAppRegistration("web")
            .AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");

        Assert.Equal(firstScope.Resource.Id, secondScope.Resource.Id);
        Assert.Equal(AzureEntraIdScopeResource.CreateScopeId("web", "access_as_user"), firstScope.Resource.Id);
    }

    [Fact]
    public void AddAppRole_registers_child_with_app_registration_parent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var role = app.AddAppRole("writer", AllowedMemberTypes.User, "Writer", "Writer", "Can write data");

        Assert.IsType<AzureEntraIdAppRoleResource>(role.Resource);
        Assert.Same(app.Resource, role.Resource.Parent);
        Assert.Equal("Writer", role.Resource.Value);
        Assert.Equal(AllowedMemberTypes.User, role.Resource.AllowedMemberTypes);
        Assert.Single(app.Resource.AppRoles, role.Resource);
    }

    [Fact]
    public void AddAppRole_application_and_combined_flags()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var daemon = app.AddAppRole(
            "daemon",
            AllowedMemberTypes.Application,
            "Daemon.Access",
            "Daemon access",
            "App-only access");
        var both = app.AddAppRole(
            "access",
            AllowedMemberTypes.User | AllowedMemberTypes.Application,
            "Access",
            "Access",
            "Users and apps");

        Assert.Equal(AllowedMemberTypes.Application, daemon.Resource.AllowedMemberTypes);
        Assert.Equal(
            AllowedMemberTypes.User | AllowedMemberTypes.Application,
            both.Resource.AllowedMemberTypes);
    }

    [Fact]
    public void AddAppRole_zero_flags_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");

        var ex = Assert.Throws<ArgumentException>(() =>
            app.AddAppRole("writer", (AllowedMemberTypes)0, "Writer", "Writer", "Can write data"));
        Assert.Equal("allowedMemberTypes", ex.ParamName);
    }

    [Fact]
    public void AddAppRole_duplicate_value_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        app.AddAppRole("writer", AllowedMemberTypes.User, "Writer", "Writer", "Can write data");

        var ex = Assert.Throws<ArgumentException>(() =>
            app.AddAppRole("writer-again", AllowedMemberTypes.User, "Writer", "Writer", "Can write data"));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void GetBicepTemplateString_create_emits_app_roles()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var writer = app.AddAppRole("writer", AllowedMemberTypes.User, "Writer", "Writer", "Can write data");
        app.AddAppRole(
            "daemon",
            AllowedMemberTypes.Application,
            "Daemon.Access",
            "Daemon access",
            "App-only access");
        app.AddAppRole(
            "access",
            AllowedMemberTypes.User | AllowedMemberTypes.Application,
            "Access",
            "Access",
            "Users and apps");

        var bicep = app.Resource.GetBicepTemplateString();
        var expectedId = writer.Resource.Id.ToString("D");

        Assert.Contains("appRoles:", bicep, StringComparison.Ordinal);
        Assert.Contains("value: 'Writer'", bicep, StringComparison.Ordinal);
        Assert.Contains("displayName: 'Writer'", bicep, StringComparison.Ordinal);
        Assert.Contains("description: 'Can write data'", bicep, StringComparison.Ordinal);
        Assert.Contains($"id: '{expectedId}'", bicep, StringComparison.Ordinal);
        Assert.Contains("isEnabled: true", bicep, StringComparison.Ordinal);
        Assert.Contains("value: 'Daemon.Access'", bicep, StringComparison.Ordinal);
        Assert.Contains("value: 'Access'", bicep, StringComparison.Ordinal);
        Assert.Matches(@"allowedMemberTypes:\s*\[\s*'User'\s*\]", bicep);
        Assert.Matches(@"allowedMemberTypes:\s*\[\s*'Application'\s*\]", bicep);
        Assert.Matches(@"allowedMemberTypes:\s*\[\s*'User'\s*'Application'\s*\]", bicep);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_app_roles()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web")
            .RunAsExisting("already-registered", resourceGroup: null!);
        app.AddAppRole("writer", AllowedMemberTypes.User, "Writer", "Writer", "Can write data");

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("appRoles:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("Writer", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAppRole_graph_id_is_stable_across_builders_and_distinct_from_scope()
    {
        var first = DistributedApplication.CreateBuilder();
        var firstRole = first.AddAzureAppRegistration("web")
            .AddAppRole("writer", AllowedMemberTypes.User, "access_as_user", "Access", "Access");

        var second = DistributedApplication.CreateBuilder();
        var secondApp = second.AddAzureAppRegistration("web");
        var secondRole = secondApp.AddAppRole("writer", AllowedMemberTypes.User, "access_as_user", "Access", "Access");
        var scope = secondApp.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");

        Assert.Equal(firstRole.Resource.Id, secondRole.Resource.Id);
        Assert.Equal(AzureEntraIdAppRoleResource.CreateAppRoleId("web", "access_as_user"), firstRole.Resource.Id);
        Assert.NotEqual(scope.Resource.Id, secondRole.Resource.Id);
    }

    [Fact]
    public void Scope_and_app_role_implement_permissible_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("web");
        var scope = app.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        var role = app.AddAppRole("writer", AllowedMemberTypes.User, "Writer", "Writer", "Can write data");

        IAzureEntraIdPermissibleResource permissibleScope = scope.Resource;
        IAzureEntraIdPermissibleResource permissibleRole = role.Resource;

        Assert.Equal(AzureEntraIdPermissionType.Scope, permissibleScope.Type);
        Assert.Equal(AzureEntraIdPermissionType.Role, permissibleRole.Type);
        Assert.Equal(scope.Resource.Id, permissibleScope.Id);
        Assert.Equal(role.Resource.Id, permissibleRole.Id);
        Assert.Same(app.Resource, permissibleScope.Parent);
        Assert.Same(app.Resource, permissibleRole.Parent);
    }

    [Fact]
    public void WithPermission_registers_required_resource_access_and_waits_for_exposer()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddAzureAppRegistration("api");
        var scope = api.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        var spa = builder.AddAzureAppRegistration("spa")
            .WithPermission(scope);

        Assert.Single(spa.Resource.RequiredPermissions, scope.Resource);
        var stored = Assert.IsType<BicepOutputReference>(
            spa.Resource.Parameters[AzureEntraIdAppRegistrationResource.ResourceAppIdParameterName("api")]);
        Assert.Equal(api.Resource.ClientId.ValueExpression, stored.ValueExpression);
        Assert.Contains(
            spa.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, api.Resource));
    }

    [Fact]
    public void WithPermission_duplicate_id_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddAzureAppRegistration("api");
        var scope = api.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        var spa = builder.AddAzureAppRegistration("spa")
            .WithPermission(scope);

        var ex = Assert.Throws<ArgumentException>(() => spa.WithPermission(scope));
        Assert.Equal("permission", ex.ParamName);
    }

    [Fact]
    public void WithPermission_self_does_not_wait()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddAzureAppRegistration("api");
        var scope = api.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        api.WithPermission(scope);

        Assert.DoesNotContain(
            api.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, api.Resource));
        Assert.False(api.Resource.Parameters.ContainsKey(AzureEntraIdAppRegistrationResource.ResourceAppIdParameterName("api")));
    }

    [Fact]
    public void GetBicepTemplateString_create_emits_required_resource_access()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddAzureAppRegistration("api");
        var scope = api.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        var role = api.AddAppRole("daemon", AllowedMemberTypes.Application, "Daemon.Access", "Daemon access", "App-only access");
        var spa = builder.AddAzureAppRegistration("spa")
            .WithPermission(scope)
            .WithPermission(role);

        var bicep = spa.Resource.GetBicepTemplateString();
        var parameterName = AzureEntraIdAppRegistrationResource.ResourceAppIdParameterName("api");
        var expectedScopeId = scope.Resource.Id.ToString("D");
        var expectedRoleId = role.Resource.Id.ToString("D");

        Assert.Contains("requiredResourceAccess:", bicep, StringComparison.Ordinal);
        Assert.Contains($"param {parameterName} string", bicep, StringComparison.Ordinal);
        Assert.Contains($"resourceAppId: {parameterName}", bicep, StringComparison.Ordinal);
        Assert.Contains($"id: '{expectedScopeId}'", bicep, StringComparison.Ordinal);
        Assert.Contains("type: 'Scope'", bicep, StringComparison.Ordinal);
        Assert.Contains($"id: '{expectedRoleId}'", bicep, StringComparison.Ordinal);
        Assert.Contains("type: 'Role'", bicep, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(bicep, "resourceAppId:"));
    }

    [Fact]
    public void GetBicepTemplateString_self_permission_uses_app_appId()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddAzureAppRegistration("api");
        var scope = api.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        api.WithPermission(scope);

        var bicep = api.Resource.GetBicepTemplateString();

        Assert.Contains("requiredResourceAccess:", bicep, StringComparison.Ordinal);
        Assert.Contains("resourceAppId: app.appId", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("param resourceAppId_", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_required_resource_access()
    {
        var builder = DistributedApplication.CreateBuilder();
        var api = builder.AddAzureAppRegistration("api");
        var scope = api.AddScope("access", "access_as_user", "Access API", "Allows the app to access the API");
        var spa = builder.AddAzureAppRegistration("spa")
            .WithPermission(scope)
            .RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = spa.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("requiredResourceAccess:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceAccess:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void AddCertificate_registers_store_check_resource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cert = AddTestCertificate(builder, "api-cert", "aa bb cc");

        Assert.Equal("api-cert", cert.Resource.Name);
        Assert.IsType<AsymmetricX509CertResource>(cert.Resource);
        Assert.Equal("AABBCC", cert.Resource.Thumbprint);
        Assert.Equal("api-cert-thumbprint", cert.Resource.ThumbprintParameter.Name);
        Assert.Equal(StoreLocation.CurrentUser, cert.Resource.StoreLocation);
    }

    [Fact]
    public void WithKeyCredential_waits_for_certificate()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cert = AddTestCertificate(builder, "api-cert", "aabbcc");
        var app = builder.AddAzureAppRegistration("api")
            .WithKeyCredential(cert);

        Assert.Single(app.Resource.KeyCredentials, cert.Resource);
        var parameterName = AsymmetricX509CertResource.KeyCredentialParameterName("api-cert");
        Assert.True(app.Resource.Parameters.ContainsKey(parameterName));
        Assert.Contains(
            app.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, cert.Resource));
    }

    [Fact]
    public void WithKeyCredential_duplicate_thumbprint_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cert = AddTestCertificate(builder, "api-cert", "aabbcc");
        var app = builder.AddAzureAppRegistration("api")
            .WithKeyCredential(cert);

        var ex = Assert.Throws<ArgumentException>(() => app.WithKeyCredential(cert));
        Assert.Equal("certificate", ex.ParamName);
    }

    [Fact]
    public void GetBicepTemplateString_create_emits_key_credentials()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cert = AddTestCertificate(builder, "api-cert", "aabbcc");
        var app = builder.AddAzureAppRegistration("api")
            .WithKeyCredential(cert);

        var bicep = app.Resource.GetBicepTemplateString();
        var parameterName = AsymmetricX509CertResource.KeyCredentialParameterName("api-cert");

        Assert.Contains("keyCredentials:", bicep, StringComparison.Ordinal);
        Assert.Contains("type: 'AsymmetricX509Cert'", bicep, StringComparison.Ordinal);
        Assert.Contains("usage: 'Verify'", bicep, StringComparison.Ordinal);
        Assert.Contains("displayName: 'api-cert'", bicep, StringComparison.Ordinal);
        Assert.Contains($"key: {parameterName}", bicep, StringComparison.Ordinal);
        Assert.Contains($"@secure()", bicep, StringComparison.Ordinal);
        Assert.Contains($"param {parameterName} string", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_key_credentials()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cert = AddTestCertificate(builder, "api-cert", "aabbcc");
        var app = builder.AddAzureAppRegistration("api")
            .WithKeyCredential(cert)
            .RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("keyCredentials:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("AsymmetricX509Cert", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void AddWebApplication_registers_child_with_app_registration_parent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("api");
        var web = app.AddWebApplication("swagger");

        Assert.IsType<AzureEntraIdWebApplicationResource>(web.Resource);
        Assert.Same(app.Resource, web.Resource.Parent);
        Assert.Single(app.Resource.WebApplications, web.Resource);
        Assert.IsNotAssignableFrom<AzureProvisioningResource>(web.Resource);
    }

    [Fact]
    public void WithRedirectUri_on_web_application_merges_into_parent_parameter()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("api")
            .WithRedirectUri("https://localhost/signin-oidc");
        var web = app.AddWebApplication("swagger")
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"))
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));

        var uris = Assert.IsType<string[]>(app.Resource.Parameters[AzureEntraIdAppRegistrationResource.RedirectUrisParameter]);
        Assert.Equal(
            ["https://localhost/signin-oidc", "https://localhost/swagger/oauth2-redirect.html"],
            uris);
        Assert.Equal(2, web.Resource.RedirectUris.Count);
    }

    [Fact]
    public void GetBicepTemplateString_create_emits_web_redirect_uris_and_implicit_grant()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("api");
        app.AddWebApplication("swagger")
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));

        var bicep = app.Resource.GetBicepTemplateString();

        Assert.Contains("redirectUris: redirectUris", bicep, StringComparison.Ordinal);
        Assert.Contains("implicitGrantSettings:", bicep, StringComparison.Ordinal);
        Assert.Contains("enableAccessTokenIssuance: true", bicep, StringComparison.Ordinal);
        Assert.Contains("enableIdTokenIssuance: true", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_without_web_uris_omits_implicit_grant()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("api");

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.Contains("redirectUris: redirectUris", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("implicitGrantSettings:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_web()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("api");
        app.AddWebApplication("swagger")
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));
        app.RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("implicitGrantSettings:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("redirectUris:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithMicrosoftIdentityWebApplication_sets_environment_and_waits()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["Azure:TenantId"] = "11111111-1111-1111-1111-111111111111";
        var cert = AddTestCertificate(builder, "api-cert", "aabbcc");
        var app = builder.AddAzureAppRegistration("api")
            .WithDefaultIdentifierUri()
            .WithKeyCredential(cert);
        var web = app.AddWebApplication("swagger")
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));
        var container = builder.AddContainer("svc", "redis")
            .WithMicrosoftIdentityWebApplication(EntraIdInstance.Workforce, web);

        Assert.Contains(
            container.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, app.Resource));
        Assert.Contains(
            container.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, cert.Resource));

        var env = await ResolveEnvironmentAsync(builder, container.Resource);
        Assert.Equal("https://login.microsoftonline.com/", env["AzureAd__Instance"]);
        Assert.Equal("11111111-1111-1111-1111-111111111111", env["AzureAd__TenantId"]);
        Assert.Equal("StoreWithThumbprint", env["AzureAd__ClientCredentials__0__SourceType"]);
        Assert.Equal("CurrentUser/My", env["AzureAd__ClientCredentials__0__CertificateStorePath"]);
        Assert.True(env.ContainsKey("AzureAd__ClientId"));
        Assert.True(env.ContainsKey("AzureAd__Audience"));
        Assert.True(env.ContainsKey("AzureAd__ClientCredentials__0__CertificateThumbprint"));
    }

    [Fact]
    public void AddSpaApplication_registers_child_with_app_registration_parent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");
        var spa = app.AddSpaApplication("spa-app");

        Assert.IsType<AzureEntraIdSpaApplicationResource>(spa.Resource);
        Assert.Same(app.Resource, spa.Resource.Parent);
        Assert.Single(app.Resource.SpaApplications, spa.Resource);
        Assert.IsNotAssignableFrom<AzureProvisioningResource>(spa.Resource);
    }

    [Fact]
    public void WithRedirectUri_on_spa_application_merges_into_parent_parameter()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");
        var spa = app.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"))
            .WithRedirectUri(new Uri("http://localhost/"));

        var uris = Assert.IsType<string[]>(app.Resource.Parameters[AzureEntraIdAppRegistrationResource.SpaRedirectUrisParameter]);
        Assert.Equal(["http://localhost/"], uris);
        Assert.Equal(2, spa.Resource.RedirectUris.Count);
    }

    [Fact]
    public void GetBicepTemplateString_create_emits_spa_redirect_uris_without_implicit_grant()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");
        app.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"));

        var bicep = app.Resource.GetBicepTemplateString();

        Assert.Contains("spa:", bicep, StringComparison.Ordinal);
        Assert.Contains("redirectUris: spaRedirectUris", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("implicitGrantSettings:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_without_spa_uris_omits_spa()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("spa:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("spaRedirectUris", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void GetBicepTemplateString_existing_omits_spa()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");
        app.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"));
        app.RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("spa:", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("spaRedirectUris", bicep, StringComparison.Ordinal);
        Assert.DoesNotContain("redirectUris:", bicep, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithEntraIdSpaApplication_sets_environment_and_waits()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.Configuration["Azure:TenantId"] = "11111111-1111-1111-1111-111111111111";
        var cert = AddTestCertificate(builder, "api-cert", "aabbcc");
        var api = builder.AddAzureAppRegistration("api")
            .WithDefaultIdentifierUri()
            .WithKeyCredential(cert);
        var access = api.AddScope(
            "access",
            "access_as_user",
            "Access API",
            "Allows the app to access the API");
        var spa = builder.AddAzureAppRegistration("spa")
            .WithDefaultIdentifierUri()
            .WithPermission(access);
        var spaApp = spa.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"));
        var container = builder.AddContainer("frontend", "nginx")
            .WithEntraIdSpaApplication(EntraIdInstance.Workforce, spaApp);

        Assert.Contains(
            container.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, spa.Resource));
        Assert.DoesNotContain(
            container.Resource.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, cert.Resource));

        var env = await ResolveEnvironmentAsync(builder, container.Resource);
        Assert.Equal("https://login.microsoftonline.com/", env["ENTRA_Instance"]);
        Assert.Equal("11111111-1111-1111-1111-111111111111", env["ENTRA_TenantId"]);
        Assert.True(env.ContainsKey("ENTRA_ClientId"));
        Assert.True(env.ContainsKey("ENTRA_Audience"));
        Assert.Equal(AzureEntraIdHostingExtensions.SpaLoginScopes, env["ENTRA_LoginScopes"]);
        Assert.True(env.ContainsKey("ENTRA_Scope"));
        Assert.DoesNotContain(env.Keys, static key => key.Contains("ClientCredentials", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithEntraIdSpaApplication_ciam_string_sets_instance()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");
        var spaApp = app.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"));
        var container = builder.AddContainer("frontend", "nginx")
            .WithEntraIdSpaApplication(EntraIdInstance.Ciam("contoso"), spaApp);

        var env = await ResolveEnvironmentAsync(builder, container.Resource);
        Assert.Equal("https://contoso.ciamlogin.com/", env["ENTRA_Instance"]);
        Assert.Equal(AzureEntraIdHostingExtensions.SpaLoginScopes, env["ENTRA_LoginScopes"]);
    }

    [Fact]
    public async Task WithMicrosoftIdentityWebApplication_ciam_string_sets_instance()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("api");
        var web = app.AddWebApplication("swagger")
            .WithRedirectUri(new Uri("https://localhost/swagger/oauth2-redirect.html"));
        var container = builder.AddContainer("svc", "redis")
            .WithMicrosoftIdentityWebApplication(EntraIdInstance.Ciam(" contoso "), web);

        var env = await ResolveEnvironmentAsync(builder, container.Resource);
        Assert.Equal("https://contoso.ciamlogin.com/", env["AzureAd__Instance"]);
    }

    [Fact]
    public async Task WithEntraIdSpaApplication_ciam_parameter_interpolates_instance()
    {
        var builder = DistributedApplication.CreateBuilder();
        var subdomain = builder.AddParameter("ciam-subdomain", "contoso");
        var app = builder.AddAzureAppRegistration("spa");
        var spaApp = app.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"));
        var container = builder.AddContainer("frontend", "nginx")
            .WithEntraIdSpaApplication(EntraIdInstance.Ciam(subdomain), spaApp);

        var env = await ResolveEnvironmentAsync(builder, container.Resource);
        Assert.Contains("ciamlogin.com", env["ENTRA_Instance"], StringComparison.Ordinal);
        Assert.Contains("ciam-subdomain", env["ENTRA_Instance"], StringComparison.Ordinal);
        Assert.DoesNotContain("contoso", env["ENTRA_Instance"], StringComparison.Ordinal);
    }

    [Fact]
    public void Ciam_rejects_empty_and_url_subdomains()
    {
        Assert.Throws<ArgumentException>(() => EntraIdInstance.Ciam(""));
        Assert.Throws<ArgumentException>(() => EntraIdInstance.Ciam("   "));
        Assert.Throws<ArgumentException>(() => EntraIdInstance.Ciam("https://contoso.ciamlogin.com/"));
        Assert.Throws<ArgumentException>(() => EntraIdInstance.Ciam("contoso/federation"));
    }

    [Fact]
    public void WithEntraIdSpaApplication_throws_when_instance_is_null()
    {
        var builder = DistributedApplication.CreateBuilder();
        var app = builder.AddAzureAppRegistration("spa");
        var spaApp = app.AddSpaApplication("spa-app")
            .WithRedirectUri(new Uri("http://localhost/"));

        Assert.Throws<ArgumentNullException>(() =>
            builder.AddContainer("frontend", "nginx")
                .WithEntraIdSpaApplication(null!, spaApp));
    }

    [Fact]
    public void TryFind_returns_false_for_missing_thumbprint()
    {
        Assert.False(AsymmetricX509CertResource.TryFind(
            "0000000000000000000000000000000000000000",
            StoreLocation.CurrentUser,
            out var certificate));
        Assert.Null(certificate);
    }

    [Fact]
    public void TryFind_returns_true_when_cert_is_in_current_user_store()
    {
        using var created = CreateEphemeralCertificate();
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(created);
        try
        {
            Assert.True(AsymmetricX509CertResource.TryFind(created.Thumbprint, StoreLocation.CurrentUser, out var found));
            using (found)
            {
                Assert.NotNull(found);
                Assert.Equal(created.Thumbprint, found!.Thumbprint, StringComparer.OrdinalIgnoreCase);
            }
        }
        finally
        {
            store.Remove(created);
        }
    }

    private static async Task<Dictionary<string, string>> ResolveEnvironmentAsync(
        IDistributedApplicationBuilder builder,
        IResource resource)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, resource);
            await annotation.Callback(context).ConfigureAwait(false);
            foreach (var pair in context.EnvironmentVariables)
            {
                values[pair.Key] = pair.Value switch
                {
                    ReferenceExpression expression => expression.ValueExpression,
                    _ => pair.Value?.ToString() ?? string.Empty
                };
            }
        }

        return values;
    }

    private static X509Certificate2 CreateEphemeralCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=neox-entra-test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx),
            password: null,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    }

    private static IResourceBuilder<AsymmetricX509CertResource> AddTestCertificate(
        IDistributedApplicationBuilder builder,
        string name,
        string thumbprint)
    {
        var parameter = builder.AddParameter($"{name}-thumbprint", thumbprint, secret: true);
        return builder.AddCertificate(name, parameter, StoreLocation.CurrentUser);
    }

    private static int CountOccurrences(string value, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}
