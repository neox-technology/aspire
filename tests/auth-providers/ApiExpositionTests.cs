using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class ApiExpositionTests
{
    [Theory]
    [InlineData(AllowedMemberType.UsersAndGroups, new[] { "User" })]
    [InlineData(AllowedMemberType.Applications, new[] { "Application" })]
    [InlineData(AllowedMemberType.Both, new[] { "User", "Application" })]
    public void AllowedMemberType_Mapping_RoundTrips(AllowedMemberType type, string[] graph)
    {
        Assert.Equal(graph, AllowedMemberTypeMapping.ToGraph(type));
        Assert.Equal(type, AllowedMemberTypeMapping.FromGraph(graph));
    }

    [Fact]
    public void WithApiExposition_DefaultUri_SetsClientIdTemplateAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(b =>
            {
                scope = b.AddScopeWithAdminConsent(
                    "access_as_user", "Access", "Access the API");
            });

        var uri = Assert.Single(api.Resource.Annotations.OfType<ApiIdentifierUriAnnotation>());
        Assert.True(uri.UseClientIdTemplate);
        Assert.Null(uri.LiteralUri);

        Assert.NotNull(scope);
        Assert.Equal("access_as_user", scope!.Resource.ScopeValue);
        Assert.False(scope.Resource.AllowUserConsent);
        Assert.Contains(
            api.Resource.Annotations.OfType<ExposedApiAnnotation>(),
            a => ReferenceEquals(a.Exposition, scope.Resource));
    }

    [Fact]
    public void WithApiExposition_LiteralUri_And_AdminAndUserConsent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition("api://my-api", b =>
            {
                scope = b.AddScopeWithAdminAndUserConsent(
                    "Files.Read",
                    "Read files",
                    "Allows reading files",
                    "Read your files",
                    "Allow the app to read your files");
            });

        var uri = Assert.Single(api.Resource.Annotations.OfType<ApiIdentifierUriAnnotation>());
        Assert.False(uri.UseClientIdTemplate);
        Assert.Equal("api://my-api", uri.LiteralUri);

        Assert.NotNull(scope);
        Assert.True(scope!.Resource.AllowUserConsent);
        Assert.Equal("Read your files", scope.Resource.UserConsentDisplayName);
    }

    [Fact]
    public void WithApiExposition_InvalidUri_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var api = entra.AddAppRegistration("api", "Api");

        Assert.Throws<ArgumentException>(() =>
            api.WithApiExposition("not-a-uri", _ => { }));
    }

    [Fact]
    public void WithAppRoleExposition_AddsRoleResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var api = entra.AddAppRegistration("api", "Api");

        var role = api.WithAppRoleExposition(
            AllowedMemberType.Applications, "Api.Caller", "Caller apps");

        Assert.Equal("Api.Caller", role.Resource.Value);
        Assert.Equal(AllowedMemberType.Applications, role.Resource.AllowedMemberType);
        Assert.Contains(
            api.Resource.Annotations.OfType<ExposedApiAnnotation>(),
            a => ReferenceEquals(a.Exposition, role.Resource));
    }

    [Fact]
    public void WithApiPermission_RecordsAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(b =>
            {
                scope = b.AddScopeWithAdminConsent("s", "S", "S desc");
            });

        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!);

        var permission = Assert.Single(web.Resource.Annotations.OfType<ApiPermissionAnnotation>());
        Assert.Same(scope!.Resource, permission.Exposition);
    }

    [Fact]
    public void CollectDesired_IdentifierUriScopesAndRoles()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition("api://contoso", b =>
            {
                scope = b.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        api.WithAppRoleExposition(AllowedMemberType.Both, "Api.Admin", "Admins");

        var uris = EntraApiExpositionApplicator.CollectDesiredIdentifierUris(api.Resource);
        Assert.Equal(["api://contoso"], uris.Select(u => u.Uri));

        var scopes = EntraApiExpositionApplicator.CollectDesiredScopes(api.Resource);
        Assert.Equal("access_as_user", Assert.Single(scopes).Value);
        Assert.Equal("Admin", scopes[0].Type);

        var roles = EntraApiExpositionApplicator.CollectDesiredAppRoles(api.Resource);
        var role = Assert.Single(roles);
        Assert.Equal("Api.Admin", role.Value);
        Assert.Equal(["User", "Application"], role.AllowedMemberTypes);
    }

    [Fact]
    public void BuildAdoptPlan_PlansApiExpositionUpdates_WhenDifferent()
    {
        var scopeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var existing = new Application
        {
            Id = "obj-1",
            AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            DisplayName = "Api",
            SignInAudience = "AzureADMyOrg",
            IdentifierUris = [],
            Api = new ApiApplication { Oauth2PermissionScopes = [] },
            AppRoles = []
        };

        var plan = EntraGraphAppProvisioner.BuildAdoptPlan(
            "tenant-1",
            "Api",
            "AzureADMyOrg",
            existing,
            [],
            [new AuthDesiredIdentifierUri { Uri = "api://my-api" }],
            [
                new AuthDesiredOauth2PermissionScope
                {
                    Id = scopeId,
                    Value = "access_as_user",
                    AdminConsentDisplayName = "Access",
                    AdminConsentDescription = "Desc",
                    Type = "Admin"
                }
            ],
            [
                new AuthDesiredAppRole
                {
                    Id = Guid.NewGuid(),
                    Value = "Api.Caller",
                    DisplayName = "Api.Caller",
                    Description = "Callers",
                    AllowedMemberTypes = ["Application"]
                }
            ]);

        Assert.Contains(AuthAppRegistrationPlanAction.UpdateIdentifierUris, plan.Actions);
        Assert.Contains(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes, plan.Actions);
        Assert.Contains(AuthAppRegistrationPlanAction.UpdateAppRoles, plan.Actions);
        Assert.False(plan.IsNoOp);
    }

    [Fact]
    public void BuildAdoptPlan_RemapsScopeId_AndNoOp_WhenMatching()
    {
        var graphScopeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var modelScopeId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var existing = new Application
        {
            Id = "obj-1",
            AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            DisplayName = "Api",
            SignInAudience = "AzureADMyOrg",
            IdentifierUris = ["api://aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"],
            Api = new ApiApplication
            {
                Oauth2PermissionScopes =
                [
                    new PermissionScope
                    {
                        Id = graphScopeId,
                        Value = "access_as_user",
                        AdminConsentDisplayName = "Access",
                        AdminConsentDescription = "Desc",
                        Type = "Admin",
                        IsEnabled = true
                    }
                ]
            },
            AppRoles = []
        };

        var plan = EntraGraphAppProvisioner.BuildAdoptPlan(
            "tenant-1",
            "Api",
            "AzureADMyOrg",
            existing,
            [],
            [new AuthDesiredIdentifierUri { Uri = AuthDesiredIdentifierUri.ClientIdTemplateMarker }],
            [
                new AuthDesiredOauth2PermissionScope
                {
                    Id = modelScopeId,
                    Value = "access_as_user",
                    AdminConsentDisplayName = "Access",
                    AdminConsentDescription = "Desc",
                    Type = "Admin"
                }
            ]);

        Assert.True(plan.IsNoOp);
        Assert.Equal(graphScopeId, Assert.Single(plan.DesiredScopes).Id);
    }

    [Fact]
    public void BuildAdoptPlan_PlansRequiredResourceAccess_WhenMissing()
    {
        var permId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var existing = new Application
        {
            Id = "obj-1",
            AppId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            DisplayName = "Web",
            SignInAudience = "AzureADMyOrg",
            RequiredResourceAccess = []
        };

        var plan = EntraGraphAppProvisioner.BuildAdoptPlan(
            "tenant-1",
            "Web",
            "AzureADMyOrg",
            existing,
            [],
            desiredPermissions:
            [
                new AuthDesiredRequiredResourceAccess
                {
                    ResourceAppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                    PermissionId = permId,
                    Type = "Scope"
                }
            ],
            hasDeclaredPermissions: true);

        Assert.Contains(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess, plan.Actions);
    }

    [Fact]
    public async Task FakeProvisioner_Plan_IncludesDesiredExposition()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition("api://sample", b =>
            {
                b.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        api.WithAppRoleExposition(AllowedMemberType.UsersAndGroups, "Reader", "Readers");

        var fake = new FakeEntraGraphAppProvisioner();
        var plan = await fake.PlanAsync(api.Resource, CancellationToken.None);

        Assert.Equal(["api://sample"], plan.DesiredIdentifierUris.Select(u => u.Uri));
        Assert.Equal("access_as_user", Assert.Single(plan.DesiredScopes).Value);
        Assert.Equal("Reader", Assert.Single(plan.DesiredAppRoles).Value);
        Assert.Contains(AuthAppRegistrationPlanAction.UpdateIdentifierUris, plan.Actions);
        Assert.Contains(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes, plan.Actions);
        Assert.Contains(AuthAppRegistrationPlanAction.UpdateAppRoles, plan.Actions);
    }
}
