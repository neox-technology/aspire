using Azure.Provisioning;
using Neox.Azure.Provisioning.Graph.Generators.Internal;
using Xunit;

namespace Neox.Azure.Provisioning.Graph.Tests;

public sealed class GraphProvisioningCodeGenTests
{
    [Fact]
    public void ToModelClassName_strips_microsoft_graph_prefix()
    {
        Assert.Equal("GraphWebApplication", GraphProvisioningCodeGen.ToModelClassName("MicrosoftGraphWebApplication"));
        Assert.Equal("GraphAddIn", GraphProvisioningCodeGen.ToModelClassName("MicrosoftGraphAddIn"));
    }

    [Fact]
    public void ToPropertyName_pascal_cases_camel_json_names()
    {
        Assert.Equal("UniqueName", GraphProvisioningCodeGen.ToPropertyName("uniqueName"));
        Assert.Equal("RedirectUris", GraphProvisioningCodeGen.ToPropertyName("redirectUris"));
    }

    [Fact]
    public void Generate_emits_application_service_principal_and_web()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "types.json"));
        var files = GraphProvisioningCodeGen.Generate(json);
        var byHint = files.ToDictionary(f => f.HintName, f => f.Source, StringComparer.Ordinal);

        Assert.Contains("GraphApplication.g.cs", byHint.Keys);
        Assert.Contains("GraphServicePrincipal.g.cs", byHint.Keys);
        Assert.Contains("GraphWebApplication.g.cs", byHint.Keys);
        Assert.Contains("GraphGroup.g.cs", byHint.Keys);
        Assert.Contains("GraphUser.g.cs", byHint.Keys);

        var application = byHint["GraphApplication.g.cs"];
        Assert.Contains("public partial class GraphApplication : ProvisionableResource", application, StringComparison.Ordinal);
        Assert.Contains("public BicepValue<string> UniqueName", application, StringComparison.Ordinal);
        Assert.DoesNotContain("public BicepValue<string> Name", application, StringComparison.Ordinal);
        Assert.Contains("FromExisting(string bicepIdentifier, BicepValue<string> uniqueName", application, StringComparison.Ordinal);
        Assert.Contains("resource.UniqueName = uniqueName;", application, StringComparison.Ordinal);
        Assert.Contains("resource.IsExistingResource = true;", application, StringComparison.Ordinal);

        var web = byHint["GraphWebApplication.g.cs"];
        Assert.Contains("public BicepList<string> RedirectUris", web, StringComparison.Ordinal);
    }
}
