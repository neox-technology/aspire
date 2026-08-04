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
    public void ClientId_BuildOptions_IncludesCreateSentinel()
    {
        var options = EntraAppRegistrationParameterPrompt.BuildOptions("AuthSample-Ops", apps: []);
        Assert.Contains(
            options,
            o => o.Key == EntraAppRegistrationParameterPrompt.CreateSentinel
                && o.Value.Contains("AuthSample-Ops", StringComparison.Ordinal));
    }

    [Fact]
    public void ModelTimeParameters_DoNotRegisterChoiceInputGenerators()
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

        Assert.Empty(tenant.Annotations.OfType<InputGeneratorAnnotation>());
        Assert.Empty(clientId.Annotations.OfType<InputGeneratorAnnotation>());
    }

    [Fact]
    public void IsCreateSentinel_TreatsEmptyAndSentinelAsCreate()
    {
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(null));
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(""));
        Assert.True(EntraAppRegistrationParameterPrompt.IsCreateSentinel(EntraAppRegistrationParameterPrompt.CreateSentinel));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCreateSentinel("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
    }
}
