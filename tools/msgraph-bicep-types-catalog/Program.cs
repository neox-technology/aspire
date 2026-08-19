using System.Net.Http.Headers;

const string ApiVersion = "v1.0";
const string TypesVersion = "1.0.0";
const string BaseUrl =
    $"https://raw.githubusercontent.com/microsoftgraph/msgraph-bicep-types/main/generated/microsoftgraph/microsoft.graph/{ApiVersion}/{TypesVersion}";

var repoRoot = FindRepoRoot();
var outputDir = Path.Combine(
    repoRoot,
    "src",
    "provisioning",
    "Neox.Azure.Provisioning.Graph",
    "generated");
Directory.CreateDirectory(outputDir);

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("neox-msgraph-bicep-types-catalog", "1.0"));

await DownloadAsync(http, $"{BaseUrl}/types.json", Path.Combine(outputDir, "types.json")).ConfigureAwait(false);
await DownloadAsync(http, $"{BaseUrl}/index.json", Path.Combine(outputDir, "index.json")).ConfigureAwait(false);

Console.WriteLine($"Wrote Graph Bicep types {ApiVersion}/{TypesVersion} to {outputDir}");

static async Task DownloadAsync(HttpClient http, string url, string path)
{
    Console.WriteLine($"Fetching {url}...");
    var bytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);
    await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
}

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
