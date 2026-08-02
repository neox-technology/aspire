using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Neox.Aspire.Hosting.Auth.EntraId.Generators.Internal;

[Generator]
public sealed class MicrosoftGraphPermissionsGenerator : IIncrementalGenerator
{
    private const string CatalogFileName = "microsoft-graph-permissions.json";

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
        PermissionsCatalogModel catalog;
        try
        {
            catalog = MicrosoftGraphPermissionsCodeGen.ParseCatalog(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEOXGRAPH001",
                    "Invalid Microsoft Graph permissions catalogue",
                    "Failed to parse microsoft-graph-permissions.json: {0}",
                    "Neox.Auth.Graph",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                location: null,
                ex.Message));
            return;
        }

        context.AddSource(
            "MicrosoftGraph.g.cs",
            SourceText.From(MicrosoftGraphPermissionsCodeGen.Generate(catalog), Encoding.UTF8));
    }
}
