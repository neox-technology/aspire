using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Lists IAM oauth clients in a project for ClientId Choice prompts.
/// </summary>
internal static class GoogleOauthClientEnumerator
{
    internal const int MaxClients = 200;
    private const string Location = "global";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<IReadOnlyList<KeyValuePair<string, string>>> TryGetOauthClientOptionsAsync(
        string projectId,
        GoogleCredential? credential = null,
        HttpClient? http = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            return [];
        }

        var ownsHttp = http is null;
        http ??= new HttpClient();
        try
        {
            credential ??= GoogleCredential.GetApplicationDefault();
            credential = credential.CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            var token = await ((ITokenAccess)credential.UnderlyingCredential)
                .GetAccessTokenForRequestAsync(authUri: null, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://iam.googleapis.com/v1/projects/{projectId}/locations/{Location}/oauthClients");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<OauthClientListDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (body?.OauthClients is null)
            {
                return [];
            }

            var result = new List<KeyValuePair<string, string>>();
            foreach (var client in body.OauthClients)
            {
                if (string.IsNullOrWhiteSpace(client.ClientId))
                {
                    continue;
                }

                var label = string.IsNullOrWhiteSpace(client.DisplayName)
                    ? client.ClientId
                    : $"{client.DisplayName} — {client.ClientId}";
                result.Add(new KeyValuePair<string, string>(client.ClientId, label));
                if (result.Count >= MaxClients)
                {
                    break;
                }
            }

            return result;
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

    private sealed class OauthClientListDto
    {
        public List<OauthClientDto>? OauthClients { get; set; }
    }

    private sealed class OauthClientDto
    {
        public string? ClientId { get; set; }
        public string? DisplayName { get; set; }
    }
}
