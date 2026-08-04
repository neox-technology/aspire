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
    public void ClientId_BuildOptions_IncludesCreateAndCustomSentinels()
    {
        var apps = new List<KeyValuePair<string, string>>
        {
            new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "Existing — aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
        };
        var options = EntraAppRegistrationParameterPrompt.BuildOptions("AuthSample-Ops", apps);

        Assert.Equal(EntraAppRegistrationParameterPrompt.CreateSentinel, options[0].Key);
        Assert.Contains("AuthSample-Ops", options[0].Value, StringComparison.Ordinal);
        Assert.Contains(options, o => o.Key == "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal(EntraAppRegistrationParameterPrompt.CustomSentinel, options[^1].Key);
        Assert.Equal(EntraAppRegistrationParameterPrompt.FormatCustomLabel(), options[^1].Value);
    }

    [Theory]
    [InlineData("__create__", null, "__create__")]
    [InlineData("Create new application", null, "__create__")]
    [InlineData("Create new application (AuthSample-Ops)", "AuthSample-Ops", "__create__")]
    [InlineData("__custom__", null, "__custom__")]
    [InlineData("Other (enter Client ID GUID)", null, "__custom__")]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", null, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [InlineData("  ", null, null)]
    [InlineData(null, null, null)]
    public void NormalizeChoiceValue_MapsKeysLabelsAndRawClientId(
        string? raw,
        string? displayName,
        string? expected)
    {
        Assert.Equal(expected, EntraAppRegistrationParameterPrompt.NormalizeChoiceValue(raw, displayName));
    }

    [Fact]
    public void IsCustomSentinel_RecognizesCustomKeyOnly()
    {
        Assert.True(EntraAppRegistrationParameterPrompt.IsCustomSentinel(EntraAppRegistrationParameterPrompt.CustomSentinel));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCustomSentinel(EntraAppRegistrationParameterPrompt.CreateSentinel));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCustomSentinel(null));
        Assert.False(EntraAppRegistrationParameterPrompt.IsCustomSentinel("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
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
