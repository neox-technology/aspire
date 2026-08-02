using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class SupportedAccountsTests
{
    [Theory]
    [InlineData(SupportedAccountsType.SingleTenant, "AzureADMyOrg")]
    [InlineData(SupportedAccountsType.MultiTenant, "AzureADMultipleOrgs")]
    [InlineData(SupportedAccountsType.MultiTenantAndPersonal, "AzureADandPersonalMicrosoftAccount")]
    [InlineData(SupportedAccountsType.PersonalMicrosoftAccount, "PersonalMicrosoftAccount")]
    public void Mapping_RoundTrips(SupportedAccountsType type, string audience)
    {
        Assert.Equal(audience, SupportedAccountsMapping.ToSignInAudience(type));
        Assert.Equal(type, SupportedAccountsMapping.FromSignInAudience(audience));
    }

    [Fact]
    public void FromSignInAudience_Unknown_DefaultsToSingleTenant()
    {
        Assert.Equal(SupportedAccountsType.SingleTenant, SupportedAccountsMapping.FromSignInAudience(null));
        Assert.Equal(SupportedAccountsType.SingleTenant, SupportedAccountsMapping.FromSignInAudience(""));
        Assert.Equal(SupportedAccountsType.SingleTenant, SupportedAccountsMapping.FromSignInAudience("SomethingElse"));
    }

    [Fact]
    public void GetDesired_WithoutAnnotation_IsSingleTenant()
    {
        var app = CreateApp();
        Assert.Equal(SupportedAccountsType.SingleTenant, SupportedAccountsMapping.GetDesired(app));
        Assert.Equal("AzureADMyOrg", SupportedAccountsMapping.GetDesiredSignInAudience(app));
    }

    [Fact]
    public void WithSupportedAccounts_SetsAndReplacesAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("entra").Entra();
        var web = entra.AddAppRegistration("web", "Web")
            .WithSupportedAccounts(SupportedAccountsType.MultiTenant);

        Assert.Equal(
            SupportedAccountsType.MultiTenant,
            Assert.Single(web.Resource.Annotations.OfType<SupportedAccountsAnnotation>()).SupportedAccounts);
        Assert.Equal("AzureADMultipleOrgs", SupportedAccountsMapping.GetDesiredSignInAudience(web.Resource));

        web.WithSupportedAccounts(SupportedAccountsType.PersonalMicrosoftAccount);

        Assert.Equal(
            SupportedAccountsType.PersonalMicrosoftAccount,
            Assert.Single(web.Resource.Annotations.OfType<SupportedAccountsAnnotation>()).SupportedAccounts);
        Assert.Equal("PersonalMicrosoftAccount", SupportedAccountsMapping.GetDesiredSignInAudience(web.Resource));
    }

    [Fact]
    public void BuildAdoptPlan_PlansUpdateSignInAudience_WhenDifferent()
    {
        var existing = new Application
        {
            Id = "obj-1",
            AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            DisplayName = "Web",
            SignInAudience = "AzureADMyOrg"
        };

        var plan = EntraGraphAppProvisioner.BuildAdoptPlan(
            "tenant-1",
            "Web",
            "AzureADMultipleOrgs",
            existing,
            []);

        Assert.Equal(AuthAppRegistrationPlanMode.Adopt, plan.Mode);
        Assert.Equal("AzureADMultipleOrgs", plan.DesiredSignInAudience);
        Assert.Equal("AzureADMyOrg", plan.Existing?.SignInAudience);
        Assert.Contains(AuthAppRegistrationPlanAction.UpdateSignInAudience, plan.Actions);
        Assert.DoesNotContain(AuthAppRegistrationPlanAction.UpdateDisplayName, plan.Actions);
        Assert.False(plan.IsNoOp);
    }

    [Fact]
    public void BuildAdoptPlan_NoOp_WhenSignInAudienceMatches()
    {
        var existing = new Application
        {
            Id = "obj-1",
            AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            DisplayName = "Web",
            SignInAudience = "AzureADMyOrg"
        };

        var plan = EntraGraphAppProvisioner.BuildAdoptPlan(
            "tenant-1",
            "Web",
            "AzureADMyOrg",
            existing,
            []);

        Assert.True(plan.IsNoOp);
        Assert.DoesNotContain(AuthAppRegistrationPlanAction.UpdateSignInAudience, plan.Actions);
    }

    [Fact]
    public async Task FakeProvisioner_Plan_IncludesDesiredSignInAudience()
    {
        var app = CreateApp();
        app.Annotations.Add(new SupportedAccountsAnnotation(SupportedAccountsType.MultiTenantAndPersonal));

        var fake = new FakeEntraGraphAppProvisioner();
        var plan = await fake.PlanAsync(app, CancellationToken.None);

        Assert.Equal("AzureADandPersonalMicrosoftAccount", plan.DesiredSignInAudience);
    }

    private static AuthAppResource CreateApp()
    {
        var provider = new EntraAuthOpsResource("entra", new AuthOpsResource("auth-ops"))
        {
            TenantIdParameter = new ParameterResource("tenant", _ => "t1", secret: false)
        };
        var app = new AuthAppResource("web", provider, "Web")
        {
            TenantIdParameter = provider.TenantIdParameter
        };
        provider.RegisterApp(app);
        return app;
    }
}
