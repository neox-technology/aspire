using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var repoRoot = FindRepoRoot();
var outputPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(
        repoRoot,
        "src",
        "hosting",
        "Neox.Aspire.Hosting.Azure.CustomDomains",
        "Provider",
        "octodns-providers.json");

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("neox-octodns-provider-catalog", "1.0"));
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

const string dockerReadmeUrl = "https://raw.githubusercontent.com/octodns/octodns-docker/main/README.md";
Console.WriteLine($"Fetching {dockerReadmeUrl}...");
var dockerReadme = await http.GetStringAsync(dockerReadmeUrl).ConfigureAwait(false);
var flavors = ParseDockerFlavors(dockerReadme);
Console.WriteLine($"Found {flavors.Count} flavors before exclusions.");

var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "octodns", "etchosts", "dyn" };
var providers = new List<ProviderEntry>();

foreach (var flavor in flavors.Where(f => !excluded.Contains(f.Slug)).OrderBy(f => f.Slug, StringComparer.Ordinal))
{
    Console.WriteLine($"Refreshing {flavor.Slug} ({flavor.Module})...");
    var readmeUrl = $"https://raw.githubusercontent.com/octodns/octodns-{flavor.Slug}/main/README.md";
    string? readme = null;
    try
    {
        readme = await http.GetStringAsync(readmeUrl).ConfigureAwait(false);
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"  WARN: failed to fetch {readmeUrl}: {ex.Message}");
        continue;
    }

    var settings = ParseConfigurationSettings(readme, out var providerClass);
    if (string.IsNullOrWhiteSpace(providerClass))
    {
        providerClass = InferProviderClass(flavor.Module, flavor.Slug);
        Console.Error.WriteLine($"  WARN: no class in README; inferred {providerClass}");
    }

    providers.Add(new ProviderEntry
    {
        Slug = flavor.Slug,
        MethodName = ToMethodName(flavor.Slug),
        ProviderClass = providerClass!,
        DockerImage = $"octodns/{flavor.Slug}",
        Module = flavor.Module,
        RepoUrl = $"https://github.com/octodns/octodns-{flavor.Slug}",
        Settings = settings
    });
}

var catalog = new CatalogDocument
{
    GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
    Source = new CatalogSource
    {
        DockerReadme = dockerReadmeUrl,
        Docs = "https://octodns.readthedocs.io/en/latest/"
    },
    Providers = providers
};

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var json = JsonSerializer.Serialize(catalog, CatalogJsonContext.Default.CatalogDocument);
await File.WriteAllTextAsync(outputPath, json + Environment.NewLine).ConfigureAwait(false);
Console.WriteLine($"Wrote {providers.Count} providers to {outputPath}");
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

static List<DockerFlavor> ParseDockerFlavors(string readme)
{
    var flavors = new List<DockerFlavor>();
    foreach (var rawLine in readme.Split('\n'))
    {
        var line = rawLine.TrimEnd('\r');
        if (!line.StartsWith('|') || line.Contains("---", StringComparison.Ordinal) || line.Contains("Flavor", StringComparison.Ordinal))
        {
            continue;
        }

        var cells = line.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (cells.Length < 2)
        {
            continue;
        }

        var flavorCell = cells[0].Trim();
        var moduleCell = cells[1].Trim();

        // "octodns (all)" → skip via slug "octodns"
        var slugMatch = Regex.Match(flavorCell, @"^`?([a-z0-9]+)(?:\s|\(|$)");
        if (!slugMatch.Success)
        {
            // plain text flavor name
            var plain = Regex.Match(flavorCell, @"^([a-z0-9]+)");
            if (!plain.Success)
            {
                continue;
            }

            slugMatch = plain;
        }

        var slug = slugMatch.Groups[1].Value;
        var moduleMatch = Regex.Match(moduleCell, @"octodns_[a-z0-9]+|octodns");
        var module = moduleMatch.Success ? moduleMatch.Value : $"octodns_{slug}";
        flavors.Add(new DockerFlavor(slug, module));
    }

    return flavors;
}

static List<SettingEntry> ParseConfigurationSettings(string readme, out string? providerClass)
{
    providerClass = null;
    var settings = new List<SettingEntry>();
    var yaml = ExtractConfigurationYaml(readme);
    if (yaml is null)
    {
        return settings;
    }

    var pendingComments = new List<string>();
    foreach (var rawLine in yaml.Split('\n'))
    {
        var line = rawLine.TrimEnd('\r');
        var trimmed = line.TrimStart();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("providers:", StringComparison.Ordinal))
        {
            pendingComments.Clear();
            continue;
        }

        // Pure comment line (no key: value) — keep for Optional / not needed hints
        if (trimmed.StartsWith('#') && !Regex.IsMatch(trimmed, @"^#\s*[a-zA-Z0-9_]+\s*:"))
        {
            pendingComments.Add(trimmed.TrimStart('#').Trim());
            continue;
        }

        // provider id line: "  cloudflare:" or "  ovh:"
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_-]+:\s*$") && !trimmed.StartsWith("class:", StringComparison.Ordinal))
        {
            pendingComments.Clear();
            continue;
        }

        var commented = trimmed.StartsWith('#');
        var content = commented ? trimmed.TrimStart('#').TrimStart() : trimmed;

        var classMatch = Regex.Match(content, @"^class:\s*(\S+)");
        if (classMatch.Success)
        {
            var cls = classMatch.Groups[1].Value.Trim('\'', '"');
            // Prefer *Provider over *Source when multiple class lines appear.
            if (providerClass is null ||
                (cls.EndsWith("Provider", StringComparison.Ordinal) &&
                 !providerClass.EndsWith("Provider", StringComparison.Ordinal)))
            {
                providerClass = cls;
            }

            pendingComments.Clear();
            continue;
        }

        var settingMatch = Regex.Match(content, @"^([a-zA-Z0-9_]+):\s*(.+)$");
        if (!settingMatch.Success)
        {
            pendingComments.Clear();
            continue;
        }

        var yamlName = settingMatch.Groups[1].Value;
        var value = settingMatch.Groups[2].Value.Trim().Trim('\'', '"');
        var hintOptional = pendingComments.Any(c =>
            c.Contains("Optional", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("not needed", StringComparison.OrdinalIgnoreCase));
        pendingComments.Clear();

        // Skip nested / unsupported structures
        if (value.StartsWith('{') || value.StartsWith('[') || value is "null")
        {
            continue;
        }

        if (value.StartsWith("env/", StringComparison.OrdinalIgnoreCase))
        {
            settings.Add(new SettingEntry
            {
                YamlName = yamlName,
                Kind = "secret",
                Required = !commented && !hintOptional,
                PropertyName = ToPropertyName(yamlName)
            });
            continue;
        }

        // Commented non-env toggles are ignored (feature flags).
        if (commented)
        {
            continue;
        }

        settings.Add(new SettingEntry
        {
            YamlName = yamlName,
            Kind = "literal",
            Required = !hintOptional,
            DefaultValue = value,
            PropertyName = ToPropertyName(yamlName)
        });
    }

    // Deduplicate by yaml name (prefer required over optional)
    return settings
        .GroupBy(s => s.YamlName, StringComparer.Ordinal)
        .Select(g => g.OrderByDescending(s => s.Required).First())
        .ToList();
}

static string? ExtractConfigurationYaml(string readme)
{
    var fences = Regex.Matches(
        readme,
        @"```ya?ml\r?\n(.*?)```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    string? bestProvider = null;
    string? fallback = null;
    foreach (Match fence in fences)
    {
        var body = fence.Groups[1].Value;
        if (!body.Contains("class:", StringComparison.Ordinal) ||
            !body.Contains("octodns", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        fallback ??= body;
        if (Regex.IsMatch(body, @"class:\s*\S*Provider\b"))
        {
            // Prefer BindProvider/Rfc2136Provider style over first Source-only block.
            bestProvider ??= body;
            if (body.Contains("BindProvider", StringComparison.Ordinal) ||
                body.Contains("Rfc2136Provider", StringComparison.Ordinal))
            {
                return body;
            }
        }
    }

    return bestProvider ?? fallback;
}

static string InferProviderClass(string module, string slug)
{
    var typeName = ToMethodName(slug) + "Provider";
    return $"{module}.{typeName}";
}

static string ToMethodName(string slug)
{
    var sb = new StringBuilder();
    var upper = true;
    char? prev = null;
    foreach (var c in slug)
    {
        if (!char.IsLetterOrDigit(c))
        {
            upper = true;
            continue;
        }

        if (prev is not null && char.IsDigit(prev.Value) && char.IsLetter(c))
        {
            upper = true;
        }

        if (upper && char.IsLetter(c))
        {
            sb.Append(char.ToUpperInvariant(c));
            upper = false;
        }
        else
        {
            sb.Append(c);
        }

        prev = c;
    }

    return sb.ToString();
}

static string ToPropertyName(string yamlName)
{
    var sb = new StringBuilder();
    var upper = true;
    foreach (var c in yamlName)
    {
        if (c is '_' or '-')
        {
            upper = true;
            continue;
        }

        if (upper && char.IsLetter(c))
        {
            sb.Append(char.ToUpperInvariant(c));
            upper = false;
        }
        else
        {
            sb.Append(c);
        }
    }

    return sb.ToString();
}

internal sealed record DockerFlavor(string Slug, string Module);

internal sealed class CatalogDocument
{
    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = "";

    [JsonPropertyName("source")]
    public CatalogSource Source { get; set; } = new();

    [JsonPropertyName("providers")]
    public List<ProviderEntry> Providers { get; set; } = [];
}

internal sealed class CatalogSource
{
    [JsonPropertyName("dockerReadme")]
    public string DockerReadme { get; set; } = "";

    [JsonPropertyName("docs")]
    public string Docs { get; set; } = "";
}

internal sealed class ProviderEntry
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = "";

    [JsonPropertyName("providerClass")]
    public string ProviderClass { get; set; } = "";

    [JsonPropertyName("dockerImage")]
    public string DockerImage { get; set; } = "";

    [JsonPropertyName("module")]
    public string Module { get; set; } = "";

    [JsonPropertyName("repoUrl")]
    public string RepoUrl { get; set; } = "";

    [JsonPropertyName("settings")]
    public List<SettingEntry> Settings { get; set; } = [];
}

internal sealed class SettingEntry
{
    [JsonPropertyName("yamlName")]
    public string YamlName { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("defaultValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("propertyName")]
    public string PropertyName { get; set; } = "";
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CatalogDocument))]
internal partial class CatalogJsonContext : JsonSerializerContext;
