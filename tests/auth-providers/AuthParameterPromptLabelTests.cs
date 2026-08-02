#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthParameterPromptLabelTests
{
    [Fact]
    public void TenantAndClientId_ChoiceLabels_IdentifyProviderAndApp()
    {
        Assert.Equal("Entra tenant — provider-entra", EntraTenantParameterPrompt.FormatLabel("provider-entra"));
        Assert.Equal(
            "Entra app — appregistration-spa (AuthSample-Ops)",
            EntraAppRegistrationParameterPrompt.FormatLabel("appregistration-spa", "AuthSample-Ops"));
    }

    [Fact]
    public void ModelTimeInputs_UseDiscriminatingLabels()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        entra.AddAppRegistration("appregistration-spa", "AuthSample-Ops");

        var tenant = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-tenant-id");
        var clientId = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-appregistration-spa-client-id");

        var tenantInput = InvokeInputGenerator(tenant);
        Assert.Equal("Entra tenant — provider-entra", tenantInput.Label);
        Assert.Contains("provider-entra", tenantInput.Description);
        Assert.Contains("provider-entra-tenant-id", tenantInput.Description);

        var clientInput = InvokeInputGenerator(clientId);
        Assert.Equal("Entra app — appregistration-spa (AuthSample-Ops)", clientInput.Label);
        Assert.Contains("appregistration-spa", clientInput.Description);
        Assert.Contains("provider-entra", clientInput.Description);
        Assert.Contains("Existing apps load after a tenant is selected", clientInput.Description);
        Assert.DoesNotContain("Graph enumeration failed", clientInput.Description);
    }

    [Fact]
    public void ClientIdChoice_ModelTime_UsesDynamicLoadingDependingOnTenant()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider-entra").Entra();
        entra.AddAppRegistration("appregistration-spa", "AuthSample-Ops");

        var clientId = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-entra-appregistration-spa-client-id");

        var clientInput = InvokeInputGenerator(clientId);
        Assert.NotNull(clientInput.DynamicLoading);
        Assert.True(clientInput.DynamicLoading.AlwaysLoadOnStart);
        Assert.Equal(
            ["provider-entra-tenant-id"],
            clientInput.DynamicLoading.DependsOnInputs);
        Assert.NotNull(clientInput.DynamicLoading.LoadCallback);
        Assert.NotNull(clientInput.Options);
        Assert.Contains(
            clientInput.Options,
            o => o.Key == EntraAppRegistrationParameterPrompt.CreateSentinel);
    }

    [Fact]
    public void IsCreateSentinel_TreatsEmptyAndSentinelAsCreate()
    {
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(null));
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(""));
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(EntraAppRegistrationParameterPrompt.CreateSentinel));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCreateSentinel("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
    }

    private static InteractionInput InvokeInputGenerator(ParameterResource parameter)
    {
        var annotation = Assert.Single(parameter.Annotations.OfType<InputGeneratorAnnotation>());
        return annotation.InputGenerator(parameter);
    }
}
