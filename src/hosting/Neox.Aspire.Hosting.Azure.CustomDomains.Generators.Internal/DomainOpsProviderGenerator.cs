using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Generators.Internal;

[Generator]
public sealed class DomainOpsProviderGenerator : IIncrementalGenerator
{
    private const string CatalogFileName = "octodns-providers.json";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var catalog = context.AdditionalTextsProvider
            .Where(static file => System.IO.Path.GetFileName(file.Path).Equals(CatalogFileName, System.StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => file.GetText(ct)?.ToString())
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Select(static (text, _) => text!);

        context.RegisterSourceOutput(catalog, static (spc, json) => Execute(spc, json));
    }

    private static void Execute(SourceProductionContext context, string json)
    {
        System.Collections.Generic.IReadOnlyList<ProviderModel> providers;
        try
        {
            providers = DomainOpsProviderCodeGen.ParseCatalog(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEOXOCTO001",
                    "Invalid OctoDNS provider catalogue",
                    "Failed to parse octodns-providers.json: {0}",
                    "Neox.OctoDNS",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                location: null,
                ex.Message));
            return;
        }

        if (providers.Count == 0)
        {
            return;
        }

        context.AddSource(
            "DomainOpsProviderBuilder.Interface.g.cs",
            SourceText.From(DomainOpsProviderCodeGen.GenerateInterface(providers), Encoding.UTF8));
        context.AddSource(
            "DomainOpsProviderBuilder.Methods.g.cs",
            SourceText.From(DomainOpsProviderCodeGen.GenerateBuilderMethods(providers), Encoding.UTF8));

        foreach (var provider in providers)
        {
            context.AddSource(
                provider.MethodName + "DomainOpsProvider.g.cs",
                SourceText.From(DomainOpsProviderCodeGen.GenerateProviderTypes(provider), Encoding.UTF8));
        }
    }
}
