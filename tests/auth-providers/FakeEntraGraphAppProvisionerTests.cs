using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class FakeEntraGraphAppProvisionerTests
{
    [Fact]
    public async Task FakeProvisioner_PlanThenProvision_ReturnsStableIds()
    {
        var provider = new EntraAuthOpsResource("entra", new AuthOpsResource("auth-ops"))
        {
            TenantIdParameter = CreateParameter("tenant", "tenant-1")
        };
        var app = new AuthAppResource("web", provider, "Web")
        {
            TenantIdParameter = provider.TenantIdParameter
        };
        provider.RegisterApp(app);

        var fake = new FakeEntraGraphAppProvisioner();
        var plan = await fake.PlanAsync(app, CancellationToken.None);
        var result = await fake.ProvisionAsync(app, plan, CancellationToken.None);

        Assert.Equal(AuthAppRegistrationPlanMode.Create, plan.Mode);
        Assert.Contains(AuthAppRegistrationPlanAction.CreateApplication, plan.Actions);
        Assert.Equal("tenant-from-param", result.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(result.ClientId));
        Assert.Null(result.ClientSecret);
    }

    [Fact]
    public async Task FakeProvisioner_AdoptPlan_BindsExistingClientId()
    {
        var provider = new EntraAuthOpsResource("entra", new AuthOpsResource("auth-ops"))
        {
            TenantIdParameter = CreateParameter("tenant", "tenant-1")
        };
        var app = new AuthAppResource("spa", provider, "Spa")
        {
            TenantIdParameter = provider.TenantIdParameter
        };
        provider.RegisterApp(app);

        var plan = new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Adopt,
            TenantId = "tenant-from-param",
            DesiredDisplayName = "Spa",
            Existing = new AuthAppRegistrationExistingSnapshot
            {
                ObjectId = "obj-1",
                AppId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                DisplayName = "Spa"
            },
            Actions = [AuthAppRegistrationPlanAction.None]
        };

        var result = await new FakeEntraGraphAppProvisioner().ProvisionAsync(app, plan, CancellationToken.None);

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", result.ClientId);
        Assert.Null(result.ClientSecret);
        Assert.True(plan.IsNoOp);
    }

    private static ParameterResource CreateParameter(string name, string value)
    {
        return new ParameterResource(name, _ => value, secret: false);
    }
}
