using System.Net.Http.Headers;
using System.Text.Json;

const string KeycloakVersion = "26.2.5";
var openApiUrl = $"https://www.keycloak.org/docs-api/{KeycloakVersion}/rest-api/openapi.json";

var repoRoot = FindRepoRoot();
var outputDir = Path.Combine(
    repoRoot,
    "src",
    "provisioning",
    "Neox.Keycloak.Provisioning.Realm",
    "generated");
Directory.CreateDirectory(outputDir);

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("neox-keycloak-realm-openapi-catalog", "1.0"));

Console.WriteLine($"Fetching {openApiUrl}...");
var openApiBytes = await http.GetByteArrayAsync(openApiUrl).ConfigureAwait(false);
var openApiPath = Path.Combine(outputDir, "openapi.json");
await File.WriteAllBytesAsync(openApiPath, openApiBytes).ConfigureAwait(false);

var index = new
{
    version = KeycloakVersion,
    sourceUrl = openApiUrl,
    fetchedAt = DateTimeOffset.UtcNow.ToString("O"),
};
var indexPath = Path.Combine(outputDir, "index.json");
await File.WriteAllTextAsync(
    indexPath,
    JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine).ConfigureAwait(false);

Console.WriteLine($"Wrote Keycloak Admin REST OpenAPI {KeycloakVersion} to {outputDir}");

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

    throw new InvalidOperationException("Could not find Neox.Aspire.slnx from the tool output directory.");
}
