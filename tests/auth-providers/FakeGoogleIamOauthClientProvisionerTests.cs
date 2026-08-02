using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class FakeGoogleIamOauthClientProvisionerTests
{
    [Fact]
    public async Task FakeProvisioner_BindPlan_ReturnsClientId()
    {
        var provider = new GoogleAuthOpsResource("google", new AuthOpsResource("auth-ops"))
        {
            ProjectIdParameter = CreateParameter("project", "proj-1")
        };
        var app = new AuthAppResource("web", provider, "Web")
        {
            TenantIdParameter = provider.ProjectIdParameter,
            ClientIdParameter = CreateParameter("client", "bound-client-id")
        };
        provider.RegisterApp(app);

        var fake = new FakeGoogleIamOauthClientProvisioner();
        var plan = await fake.PlanAsync(app, CancellationToken.None);
        var result = await fake.ProvisionAsync(app, plan, CancellationToken.None);

        Assert.Equal(GoogleOauthClientPlanMode.Bind, plan.Mode);
        Assert.Contains(GoogleOauthClientPlanAction.BindClientId, plan.Actions);
        Assert.Equal("bound-client-id", plan.ClientId);
        Assert.Equal("project-from-param", result.ProjectId);
        Assert.Equal("bound-client-id", result.ClientId);
        Assert.Null(result.ClientSecret);
    }

    [Fact]
    public async Task FakeProvisioner_CreateSentinel_ThrowsClearError()
    {
        var provider = new GoogleAuthOpsResource("google", new AuthOpsResource("auth-ops"))
        {
            ProjectIdParameter = CreateParameter("project", "proj-1")
        };
        var app = new AuthAppResource("spa", provider, "Spa")
        {
            TenantIdParameter = provider.ProjectIdParameter,
            ClientIdParameter = CreateParameter("client", GoogleOauthClientParameterPrompt.CreateSentinel)
        };
        provider.RegisterApp(app);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new FakeGoogleIamOauthClientProvisioner().PlanAsync(app, CancellationToken.None));

        Assert.Contains("does not create oauth clients", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FakeProvisioner_MissingClientId_ThrowsClearError()
    {
        var provider = new GoogleAuthOpsResource("google", new AuthOpsResource("auth-ops"))
        {
            ProjectIdParameter = CreateParameter("project", "proj-1")
        };
        var app = new AuthAppResource("spa", provider, "Spa")
        {
            TenantIdParameter = provider.ProjectIdParameter,
            ClientIdParameter = new ParameterResource("client", _ => throw new InvalidOperationException("unset"), secret: false)
        };
        provider.RegisterApp(app);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new FakeGoogleIamOauthClientProvisioner().PlanAsync(app, CancellationToken.None));

        Assert.Contains("does not create oauth clients", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ParameterResource CreateParameter(string name, string value)
    {
        return new ParameterResource(name, _ => value, secret: false);
    }
}
