using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Default Graph REST client for <c>applications/{id}/addPassword</c>.
/// </summary>
internal sealed class EntraIdAppPasswordClient : IEntraIdAppPasswordClient
{
    private static readonly TokenRequestContext GraphScope = new(["https://graph.microsoft.com/.default"]);

    private readonly TokenCredential _credential;
    private readonly HttpClient _http;

    public EntraIdAppPasswordClient(TokenCredential? credential = null, HttpClient? http = null)
    {
        _credential = credential ?? new DefaultAzureCredential();
        _http = http ?? new HttpClient();
    }

    public async Task<string> AddPasswordAsync(string objectId, string displayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(objectId);
        ArgumentException.ThrowIfNullOrEmpty(displayName);

        var token = await _credential.GetTokenAsync(GraphScope, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/applications/{Uri.EscapeDataString(objectId)}/addPassword")
        {
            Content = JsonContent.Create(new AddPasswordRequest
            {
                PasswordCredential = new PasswordCredentialBody { DisplayName = displayName }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Graph addPassword failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }

        var parsed = JsonSerializer.Deserialize<AddPasswordResponse>(body);
        if (string.IsNullOrEmpty(parsed?.SecretText))
        {
            throw new InvalidOperationException("Graph addPassword response did not include secretText.");
        }

        return parsed.SecretText;
    }

    private sealed class AddPasswordRequest
    {
        [JsonPropertyName("passwordCredential")]
        public PasswordCredentialBody PasswordCredential { get; init; } = null!;
    }

    private sealed class PasswordCredentialBody
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; init; } = null!;
    }

    private sealed class AddPasswordResponse
    {
        [JsonPropertyName("secretText")]
        public string? SecretText { get; init; }
    }
}
