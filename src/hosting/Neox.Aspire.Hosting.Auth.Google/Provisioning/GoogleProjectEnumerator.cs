using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Lists Google Cloud projects visible to ADC for ProjectId Choice prompts.
/// Uses Resource Manager REST without the ADC quota project header (which often 403s when
/// <c>cloudresourcemanager.googleapis.com</c> is not enabled on that quota project), then
/// falls back to <c>gcloud projects list</c>.
/// </summary>
internal static class GoogleProjectEnumerator
{
    internal const int MaxProjects = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<KeyValuePair<string, string>>> TryGetProjectOptionsAsync(
        GoogleCredential? credential = null,
        HttpClient? http = null,
        CancellationToken cancellationToken = default)
    {
        var fromApi = await TryListViaResourceManagerAsync(credential, http, cancellationToken)
            .ConfigureAwait(false);
        if (fromApi.Count > 0)
        {
            return fromApi;
        }

        return await TryListViaGcloudAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<KeyValuePair<string, string>>> TryListViaResourceManagerAsync(
        GoogleCredential? credential,
        HttpClient? http,
        CancellationToken cancellationToken)
    {
        var ownsHttp = http is null;
        http ??= new HttpClient();
        try
        {
            credential ??= GoogleCredential.GetApplicationDefault();
            credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform.read-only");
            var token = await ((ITokenAccess)credential.UnderlyingCredential)
                .GetAccessTokenForRequestAsync(authUri: null, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var result = new List<KeyValuePair<string, string>>();
            string? pageToken = null;
            do
            {
                var url =
                    "https://cloudresourcemanager.googleapis.com/v3/projects:search" +
                    $"?pageSize={Math.Min(100, MaxProjects - result.Count)}" +
                    "&query=" + Uri.EscapeDataString("state:ACTIVE");
                if (!string.IsNullOrEmpty(pageToken))
                {
                    url += "&pageToken=" + Uri.EscapeDataString(pageToken);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                // Intentionally omit x-goog-user-project: ADC quota_project_id often points at a
                // project where Cloud Resource Manager API is not enabled (403 SERVICE_DISABLED).

                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                var body = await response.Content
                    .ReadFromJsonAsync<ProjectSearchResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                if (body?.Projects is not null)
                {
                    foreach (var project in body.Projects)
                    {
                        var projectId = project.ProjectId;
                        if (string.IsNullOrWhiteSpace(projectId))
                        {
                            continue;
                        }

                        var label = string.IsNullOrWhiteSpace(project.DisplayName)
                            ? projectId
                            : $"{project.DisplayName} — {projectId}";
                        result.Add(new KeyValuePair<string, string>(projectId, label));
                        if (result.Count >= MaxProjects)
                        {
                            return Order(result);
                        }
                    }
                }

                pageToken = body?.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken) && result.Count < MaxProjects);

            return Order(result);
        }
        catch
        {
            return [];
        }
        finally
        {
            if (ownsHttp)
            {
                http.Dispose();
            }
        }
    }

    private static async Task<IReadOnlyList<KeyValuePair<string, string>>> TryListViaGcloudAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "gcloud",
                ArgumentList =
                {
                    "projects",
                    "list",
                    "--format=json(projectId,name)",
                    $"--limit={MaxProjects}"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return [];
            }

            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return [];
            }

            var projects = JsonSerializer.Deserialize<List<GcloudProject>>(stdout, JsonOptions);
            if (projects is null || projects.Count == 0)
            {
                return [];
            }

            var result = new List<KeyValuePair<string, string>>();
            foreach (var project in projects)
            {
                if (string.IsNullOrWhiteSpace(project.ProjectId))
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(project.Name)
                    ? project.ProjectId
                    : $"{project.Name} — {project.ProjectId}";
                result.Add(new KeyValuePair<string, string>(project.ProjectId, label));
            }

            return Order(result);
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<KeyValuePair<string, string>> Order(
        List<KeyValuePair<string, string>> options) =>
        options
            .OrderBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class ProjectSearchResponse
    {
        public List<ProjectDto>? Projects { get; set; }
        public string? NextPageToken { get; set; }
    }

    private sealed class ProjectDto
    {
        public string? ProjectId { get; set; }
        public string? DisplayName { get; set; }
    }

    private sealed class GcloudProject
    {
        public string? ProjectId { get; set; }
        public string? Name { get; set; }
    }
}
