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
        Assert.Equal("web-apiperm-s", permission.PermissionResource.Name);
        Assert.Same(web.Resource, permission.PermissionResource.Parent);
        Assert.Contains(
            permission.PermissionResource.Annotations.OfType<ResourceRelationshipAnnotation>(),
            a => a.Type == "Parent" && ReferenceEquals(a.Resource, web.Resource));
        Assert.Contains(builder.Resources, r => ReferenceEquals(r, permission.PermissionResource));
    }

    [Fact]
    public void WithApiPermission_WaitsForExposerAuthApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;
        IResourceBuilder<AppRoleApiExposition>? role = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(b =>
            {
                scope = b.AddScopeWithAdminConsent("s", "S", "S desc");
            });
        role = api.WithAppRoleExposition(AllowedMemberType.Applications, "Api.Caller", "Caller");

        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!)
            .WithApiPermission(role!);

        var waits = web.Resource.Annotations.OfType<WaitAnnotation>()
            .Where(a => ReferenceEquals(a.Resource, api.Resource))
            .ToList();
        Assert.Single(waits);
        Assert.Equal(WaitType.WaitUntilHealthy, waits[0].WaitType);
    }

    [Fact]
    public void WithApiPermission_WellKnown_DoesNotWaitForAuthApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();

        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(MicrosoftGraph.Delegated.UserRead);

        Assert.Empty(web.Resource.Annotations.OfType<WaitAnnotation>());
    }

    [Fact]
    public void WithApiPermission_SelfOwnedExposition_DoesNotSelfWait()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(b =>
            {
                scope = b.AddScopeWithAdminConsent("s", "S", "S desc");
            })
            .WithApiPermission(scope!);

        Assert.Empty(api.Resource.Annotations.OfType<WaitAnnotation>());
    }

    [Fact]
    public void WithApiPermission_WellKnown_RecordsAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();

        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(MicrosoftGraph.Delegated.UserRead)
            .WithApiPermission(MicrosoftGraph.Application.UserReadAll);

        var wellKnown = web.Resource.Annotations.OfType<WellKnownApiPermissionAnnotation>().ToList();
        Assert.Equal(2, wellKnown.Count);
        Assert.Equal("User.Read", wellKnown[0].Permission.Value);
        Assert.Equal("Scope", wellKnown[0].Permission.Type);
        Assert.Equal(MicrosoftGraph.AppId, wellKnown[0].Permission.ResourceAppId);
        Assert.Equal("User.Read.All", wellKnown[1].Permission.Value);
        Assert.Equal("Role", wellKnown[1].Permission.Type);
        Assert.Equal("web-apiperm-user-read", wellKnown[0].PermissionResource.Name);
        Assert.Equal("web-apiperm-user-read-all", wellKnown[1].PermissionResource.Name);
        Assert.Same(web.Resource, wellKnown[0].PermissionResource.Parent);
        Assert.Contains(builder.Resources, r => ReferenceEquals(r, wellKnown[0].PermissionResource));
        Assert.Contains(builder.Resources, r => ReferenceEquals(r, wellKnown[1].PermissionResource));
    }

    [Fact]
    public void RebuildDesired_RemapsPermissionIds_FromExposerGraphApplication()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition("api://aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", b =>
            {
                scope = b.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var role = api.WithAppRoleExposition(AllowedMemberType.Applications, "Api.Caller", "Callers");

        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!)
            .WithApiPermission(role);

        var graphScopeId = Guid.Parse("648a9683-c59e-4995-a45f-27b88697311f");
        var graphRoleId = Guid.Parse("93ff8454-64a6-4c91-89f2-aa170d011320");
        var orphanScopeId = Guid.Parse("6157c50d-6989-4f47-99c7-2b2b25288aed");
        var exposerApp = new Application
        {
            AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
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
            AppRoles =
            [
                new AppRole
                {
                    Id = graphRoleId,
                    Value = "Api.Caller",
                    DisplayName = "Api.Caller",
                    Description = "Callers",
                    AllowedMemberTypes = ["Application"],
                    IsEnabled = true
                }
            ]
        };

        var desired = EntraApiPermissionApplicator.RebuildDesiredWithExposerApplications(
            web.Resource,
            _ => "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            new Dictionary<string, Application>(StringComparer.OrdinalIgnoreCase)
            {
                ["aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"] = exposerApp
            });

        Assert.Equal(2, desired.Count);
        Assert.Contains(desired, d => d.PermissionId == graphScopeId && d.Type == "Scope");
        Assert.Contains(desired, d => d.PermissionId == graphRoleId && d.Type == "Role");
        Assert.DoesNotContain(desired, d => d.PermissionId == scope!.Resource.PermissionId);
        Assert.DoesNotContain(desired, d => d.PermissionId == role.Resource.RoleId);

        var existingWithOrphans = new[]
        {
            new AuthDesiredRequiredResourceAccess
            {
                ResourceAppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                PermissionId = orphanScopeId,
                Type = "Scope"
            },
            new AuthDesiredRequiredResourceAccess
            {
                ResourceAppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                PermissionId = graphScopeId,
                Type = "Scope"
            },
            new AuthDesiredRequiredResourceAccess
            {
                ResourceAppId = MicrosoftGraph.AppId,
                PermissionId = MicrosoftGraph.Delegated.UserRead.PermissionId,
                Type = "Scope"
            }
        };

        var validIds = new HashSet<Guid> { graphScopeId, graphRoleId };
        var cleaned = existingWithOrphans
            .Where(e =>
                !string.Equals(e.ResourceAppId, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", StringComparison.OrdinalIgnoreCase)
                || validIds.Contains(e.PermissionId))
            .ToList();
        var merged = EntraApiPermissionApplicator.MergeForApply(desired, cleaned);

        var apiAccess = Assert.Single(
            merged,
            r => string.Equals(r.ResourceAppId, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, apiAccess.ResourceAccess!.Count);
        Assert.DoesNotContain(apiAccess.ResourceAccess, a => a.Id == orphanScopeId);
        Assert.Contains(merged, r => r.ResourceAppId == MicrosoftGraph.AppId);
    }

    [Fact]
    public void CollectModelPermissionIds_ReturnsExpositionGuids()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        IResourceBuilder<ScopeApiExposition>? scope = null;

        var api = entra.AddAppRegistration("api", "Api")
            .WithApiExposition(b =>
            {
                scope = b.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var web = entra.AddAppRegistration("web", "Web")
            .WithApiPermission(scope!);

        var modelIds = EntraApiPermissionApplicator.CollectModelPermissionIds(web.Resource);
        Assert.Equal(scope!.Resource.PermissionId, Assert.Single(modelIds));
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
