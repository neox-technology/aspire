using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthOpsParentHierarchyTests
{
    [Fact]
    public void Hierarchy_ProviderUnderAuthOps_AppsUnderProvider_ExpositionsUnderApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();

        IResourceBuilder<ScopeApiExposition>? scope = null;
        var api = entra.AddAppRegistration("appregistration-api", "Api")
            .WithApiExposition(a =>
            {
                scope = a.AddScopeWithAdminConsent("access_as_user", "Access", "Desc");
            });
        var role = api.WithAppRoleExposition(
            AllowedMemberType.Applications, "Api.Caller", "Callers");

        var web = entra.AddAppRegistration("appregistration-web", "Web");

        var authOps = Assert.Single(builder.Resources.OfType<AuthOpsResource>());
        Assert.Equal(AuthOpsResource.DefaultResourceName, authOps.Name);

        var provider = entra.Resource.Resource;
        Assert.Same(authOps, provider.Parent);
        AssertHasParentRelationship(provider, authOps);

        Assert.Same(provider, api.Resource.Parent);
        AssertHasParentRelationship(api.Resource, provider);
        Assert.Same(provider, web.Resource.Parent);
        AssertHasParentRelationship(web.Resource, provider);

        Assert.NotNull(scope);
        Assert.Same(api.Resource, scope!.Resource.Parent);
        AssertHasParentRelationship(scope.Resource, api.Resource);
        Assert.Same(api.Resource, role.Resource.Parent);
        AssertHasParentRelationship(role.Resource, api.Resource);

        var tenant = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-tenant-id");
        AssertHasParentRelationship(tenant, provider);

        var clientId = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-appregistration-api-client-id");
        AssertHasParentRelationship(clientId, api.Resource);

        var clientSecret = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-appregistration-api-client-secret");
        AssertHasParentRelationship(clientSecret, api.Resource);
    }

    private static void AssertHasParentRelationship(IResource child, IResource parent)
    {
        Assert.Contains(
            child.Annotations.OfType<ResourceRelationshipAnnotation>(),
            a => a.Type == "Parent" && ReferenceEquals(a.Resource, parent));
    }
}
