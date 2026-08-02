using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

const string GraphAppId = "00000003-0000-0000-c000-000000000000";
const string DocsUrl =
    "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-docs-contrib/main/concepts/permissions-reference.md";

var repoRoot = FindRepoRoot();
var fromDocs = args.Any(a => string.Equals(a, "--from-docs", StringComparison.OrdinalIgnoreCase));
var outputPath = args.FirstOrDefault(a => !a.StartsWith('-')) is { } explicitPath
    ? Path.GetFullPath(explicitPath)
    : Path.Combine(
        repoRoot,
        "src",
        "hosting",
        "Neox.Aspire.Hosting.Auth.EntraId",
        "Graph",
        "microsoft-graph-permissions.json");

CatalogDocument catalog;
if (fromDocs)
{
    Console.WriteLine($"Fetching {DocsUrl}...");
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("neox-microsoft-graph-permissions-catalog", "1.0"));
    var markdown = await http.GetStringAsync(DocsUrl).ConfigureAwait(false);
    catalog = ParseFromDocs(markdown);
    catalog.Source = new CatalogSource
    {
        Mode = "docs",
        Docs = DocsUrl,
    };
}
else
{
    Console.WriteLine($"Querying Microsoft Graph service principal {GraphAppId}...");
    var credential = new DefaultAzureCredential();
    var graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    var sp = await graph.ServicePrincipalsWithAppId(GraphAppId)
        .GetAsync(config =>
        {
            config.QueryParameters.Select = ["appId", "appRoles", "oauth2PermissionScopes"];
        })
        .ConfigureAwait(false);

    if (sp is null)
    {
        Console.Error.WriteLine("Microsoft Graph service principal was not found. Use --from-docs for an offline bootstrap.");
        return 1;
    }

    catalog = FromServicePrincipal(sp);
    catalog.Source = new CatalogSource
    {
        Mode = "graph",
        Docs = "https://learn.microsoft.com/graph/permissions-reference",
    };
}

catalog.GeneratedAt = DateTimeOffset.UtcNow.ToString("O");
catalog.AppId = GraphAppId;

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(catalog, CatalogJsonContext.Default.CatalogDocument);
await File.WriteAllTextAsync(outputPath, json + Environment.NewLine).ConfigureAwait(false);
Console.WriteLine(
    $"Wrote {catalog.Delegated.Count} delegated + {catalog.Application.Count} application permissions to {outputPath}");
return 0;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Neox.Aspire.slnx")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static CatalogDocument FromServicePrincipal(ServicePrincipal sp)
{
    var catalog = new CatalogDocument();

    foreach (var scope in sp.Oauth2PermissionScopes ?? [])
    {
        if (scope.Id is null || string.IsNullOrWhiteSpace(scope.Value))
        {
            continue;
        }

        catalog.Delegated.Add(new PermissionEntry
        {
            Id = scope.Id.Value.ToString("D"),
            Value = scope.Value,
            DisplayName = scope.AdminConsentDisplayName ?? scope.UserConsentDisplayName ?? scope.Value,
            Description = scope.AdminConsentDescription ?? scope.UserConsentDescription ?? "",
            IsEnabled = scope.IsEnabled != false,
        });
    }

    foreach (var role in sp.AppRoles ?? [])
    {
        if (role.Id is null || string.IsNullOrWhiteSpace(role.Value))
        {
            continue;
        }

        if (role.AllowedMemberTypes is null ||
            !role.AllowedMemberTypes.Any(t => string.Equals(t, "Application", StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }

        catalog.Application.Add(new PermissionEntry
        {
            Id = role.Id.Value.ToString("D"),
            Value = role.Value,
            DisplayName = role.DisplayName ?? role.Value,
            Description = role.Description ?? "",
            IsEnabled = role.IsEnabled != false,
        });
    }

    catalog.Delegated = catalog.Delegated
        .OrderBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    catalog.Application = catalog.Application
        .OrderBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    return catalog;
}

static CatalogDocument ParseFromDocs(string markdown)
{
    var catalog = new CatalogDocument();
    var heading = new Regex(@"^###\s+([A-Za-z0-9._-]+)\s*$", RegexOptions.Multiline);
    var matches = heading.Matches(markdown);

    for (var i = 0; i < matches.Count; i++)
    {
        var value = matches[i].Groups[1].Value;
        var start = matches[i].Index + matches[i].Length;
        var end = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
        var section = markdown.Substring(start, end - start);

        // Skip non-permission headings (All permissions, etc.) without Identifier rows.
        if (!section.Contains("| Identifier |", StringComparison.OrdinalIgnoreCase) &&
            !section.Contains("|Identifier|", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var appId = ExtractCell(section, "Identifier", column: 1);
        var delId = ExtractCell(section, "Identifier", column: 2);
        var appDisplay = ExtractCell(section, "DisplayText", column: 1)
            ?? ExtractCell(section, "Display name", column: 1);
        var delDisplay = ExtractCell(section, "DisplayText", column: 2)
            ?? ExtractCell(section, "Display name", column: 2);
        var appDesc = ExtractCell(section, "Description", column: 1);
        var delDesc = ExtractCell(section, "Description", column: 2);

        if (TryParseGuid(appId, out var applicationId))
        {
            catalog.Application.Add(new PermissionEntry
            {
                Id = applicationId.ToString("D"),
                Value = value,
                DisplayName = appDisplay ?? value,
                Description = appDesc ?? "",
                IsEnabled = true,
            });
        }

        if (TryParseGuid(delId, out var delegatedId))
        {
            catalog.Delegated.Add(new PermissionEntry
            {
                Id = delegatedId.ToString("D"),
                Value = value,
                DisplayName = delDisplay ?? value,
                Description = delDesc ?? "",
                IsEnabled = true,
            });
        }
    }

    catalog.Delegated = catalog.Delegated
        .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .OrderBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    catalog.Application = catalog.Application
        .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
        .Select(g => g.First())
        .OrderBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
        .ToList();
    return catalog;
}

static string? ExtractCell(string section, string rowLabel, int column)
{
    // column: 1 = Application, 2 = Delegated (after the label cell)
    foreach (var rawLine in section.Split('\n'))
    {
        var line = rawLine.TrimEnd('\r').Trim();
        if (!line.StartsWith('|'))
        {
            continue;
        }

        var cells = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (cells.Length < 3)
        {
            continue;
        }

        if (!cells[0].Equals(rowLabel, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var index = column;
        if (index < 0 || index >= cells.Length)
        {
            return null;
        }

        var value = cells[index].Trim();
        if (string.IsNullOrWhiteSpace(value) ||
            value is "-" or "—" or "N/A" or "n/a")
        {
            return null;
        }

        return value;
    }

    return null;
}

static bool TryParseGuid(string? text, out Guid guid)
{
    guid = Guid.Empty;
    if (string.IsNullOrWhiteSpace(text))
    {
        return false;
    }

    return Guid.TryParse(text.Trim('`', ' ', '\t'), out guid) && guid != Guid.Empty;
}

internal sealed class CatalogDocument
{
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonPropertyName("appId")]
    public string AppId { get; set; } = "00000003-0000-0000-c000-000000000000";

    [JsonPropertyName("source")]
    public CatalogSource Source { get; set; } = new();

    [JsonPropertyName("delegated")]
    public List<PermissionEntry> Delegated { get; set; } = [];

    [JsonPropertyName("application")]
    public List<PermissionEntry> Application { get; set; } = [];
}

internal sealed class CatalogSource
{
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "";

    [JsonPropertyName("docs")]
    public string Docs { get; set; } = "";
}

internal sealed class PermissionEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CatalogDocument))]
internal partial class CatalogJsonContext : JsonSerializerContext;
