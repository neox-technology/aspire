using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Neox.Keycloak.Provisioning.Realm.Generators.Internal;

[Generator]
public sealed class KeycloakRealmProvisioningGenerator : IIncrementalGenerator
{
    private const string CatalogFileName = "openapi.json";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var catalog = context.AdditionalTextsProvider
            .Where(static file => Path.GetFileName(file.Path).Equals(CatalogFileName, StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => file.GetText(ct)?.ToString())
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Select(static (text, _) => text!);

        context.RegisterSourceOutput(catalog, static (spc, json) => Execute(spc, json));
    }

    private static void Execute(SourceProductionContext context, string json)
    {
        IReadOnlyList<(string HintName, string Source)> files;
        try
        {
            files = KeycloakRealmProvisioningCodeGen.Generate(json);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "NEOXKCRealmProv001",
                    "Invalid Keycloak realm OpenAPI catalogue",
                    "Failed to generate Keycloak realm types from openapi.json: {0}",
                    "Neox.Keycloak.Provisioning.Realm",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                location: null,
                ex.Message));
            return;
        }

        foreach (var (hintName, source) in files)
        {
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }
}
