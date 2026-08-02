using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aspire.Hosting.ApplicationModel;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Adopt/bind implementation of <see cref="IGoogleIamOauthClientProvisioner"/> — validates ClientId
/// and returns ProjectId/ClientId for parameter persistence. Does not create or patch IAM clients.
/// </summary>
public sealed class GoogleIamOauthClientProvisioner : IGoogleIamOauthClientProvisioner
{
    private const string IamScope = "https://www.googleapis.com/auth/cloud-platform";
    private const string Location = "global";
    private const string CreateOutOfScopeMessage =
        "Google AuthOps does not create oauth clients. Set ClientId via the Choice prompt " +
        "(existing client or custom paste) or Parameters__{provider}-{app}-client-id.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly GoogleCredential? _credential;
    private readonly IConfiguration? _configuration;

    public GoogleIamOauthClientProvisioner(
        HttpClient http,
        GoogleCredential? credential = null,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _credential = credential?.CreateScoped(IamScope);
        _configuration = configuration;
    }

    public static GoogleIamOauthClientProvisioner Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetService<IConfiguration>();
        var http = services.GetService<IHttpClientFactory>()?.CreateClient(nameof(GoogleIamOauthClientProvisioner))
            ?? new HttpClient();

        GoogleCredential? credential = null;
        try
        {
            credential = GoogleCredential.GetApplicationDefault();
        }
        catch
        {
            // ADC optional for bind-only when ClientId/ProjectId come from Parameters.
        }

        return new GoogleIamOauthClientProvisioner(http, credential, configuration);
    }

    public async Task<GoogleOauthClientPlan> PlanAsync(GoogleAuthAppRegistrationResource app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Provider is not GoogleAuthOpsResource google)
        {
            throw new InvalidOperationException(
                $"Auth app '{app.Name}' provider '{app.Provider.Name}' is not Google.");
        }

        var projectId = await ResolveProjectIdAsync(google, app, cancellationToken).ConfigureAwait(false);
        var clientId = ResolveBoundClientId(app)
            ?? throw new InvalidOperationException(
                $"{CreateOutOfScopeMessage} (Auth app '{app.Name}').");

        if (GoogleOauthClientParameterPrompt.IsCreateSentinel(clientId))
        {
            throw new InvalidOperationException(
                $"{CreateOutOfScopeMessage} (Auth app '{app.Name}').");
        }

        var existing = await TryFindExistingAsync(projectId, clientId, cancellationToken).ConfigureAwait(false);

        return new GoogleOauthClientPlan
        {
            Mode = GoogleOauthClientPlanMode.Bind,
            ProjectId = projectId,
            ClientId = clientId,
            DesiredDisplayName = app.DisplayName,
            Existing = existing,
            Actions = [GoogleOauthClientPlanAction.BindClientId]
        };
    }

    public Task<GoogleProvisionResult> ProvisionAsync(
        GoogleAuthAppRegistrationResource app,
        GoogleOauthClientPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(plan);
        _ = cancellationToken;

        if (string.IsNullOrWhiteSpace(plan.ClientId)
            || GoogleOauthClientParameterPrompt.IsCreateSentinel(plan.ClientId))
        {
            throw new InvalidOperationException(
                $"{CreateOutOfScopeMessage} (Auth app '{app.Name}').");
        }

        return Task.FromResult(new GoogleProvisionResult
        {
            ProjectId = plan.ProjectId,
            ClientId = plan.ClientId,
            ClientSecret = null
        });
    }

    private async Task<string> ResolveProjectIdAsync(
        GoogleAuthOpsResource google,
        GoogleAuthAppRegistrationResource app,
        CancellationToken cancellationToken)
    {
        if (app.TenantIdParameter is not null)
        {
            var fromParam = await app.TenantIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fromParam))
            {
                return fromParam!;
            }
        }

        var fromGoogle = await google.ProjectIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fromGoogle))
        {
            return fromGoogle!;
        }

        var configured =
            _configuration?["Google:ProjectId"]
            ?? Environment.GetEnvironmentVariable("Google__ProjectId")
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
            ?? Environment.GetEnvironmentVariable("GCP_PROJECT");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        throw new InvalidOperationException(
            "Google project id is required. Set Google options ProjectId parameter, Parameters__{provider}-project-id, or GOOGLE_CLOUD_PROJECT.");
    }

    private static string? ResolveBoundClientId(GoogleAuthAppRegistrationResource app)
    {
        if (app.ClientIdParameter is not null
            && TryGetResolvedParameterValue(app.ClientIdParameter, out var fromParam)
            && !string.IsNullOrWhiteSpace(fromParam))
        {
            return fromParam;
        }

        return null;
    }

    private static bool TryGetResolvedParameterValue(ParameterResource parameter, out string? value)
    {
        value = null;
        try
        {
            var prop = typeof(ParameterResource).GetProperty(
                "WaitForValueTcs",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            var tcsObj = prop?.GetValue(parameter);
            if (tcsObj is not null)
            {
                var taskProp = tcsObj.GetType().GetProperty("Task");
                if (taskProp?.GetValue(tcsObj) is Task<string> task)
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        return false;
                    }

                    value = task.Result;
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private async Task<GoogleOauthClientExistingSnapshot?> TryFindExistingAsync(
        string projectId,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (_credential is null)
        {
            return null;
        }

        try
        {
            var listed = await ListAsync(projectId, cancellationToken).ConfigureAwait(false);
            return listed.FirstOrDefault(c =>
                string.Equals(c.ClientId, clientId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.OauthClientResourceId, clientId, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<GoogleOauthClientExistingSnapshot>> ListAsync(
        string projectId,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
                HttpMethod.Get,
                $"https://iam.googleapis.com/v1/projects/{projectId}/locations/{Location}/oauthClients",
                cancellationToken)
            .ConfigureAwait(false);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var body = await response.Content.ReadFromJsonAsync<OauthClientListDto>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return body?.OauthClients?.Select(ToSnapshot).Where(s => s is not null).Cast<GoogleOauthClientExistingSnapshot>().ToList()
            ?? [];
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string url,
        CancellationToken cancellationToken)
    {
        if (_credential is null)
        {
            throw new InvalidOperationException("Google ADC is required for IAM list/get.");
        }

        var token = await ((ITokenAccess)_credential.UnderlyingCredential)
            .GetAccessTokenForRequestAsync(authUri: null, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static GoogleOauthClientExistingSnapshot? ToSnapshot(OauthClientDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientId))
        {
            return null;
        }

        return new GoogleOauthClientExistingSnapshot
        {
            OauthClientResourceId = ExtractResourceId(dto.Name) ?? dto.ClientId,
            ClientId = dto.ClientId,
            DisplayName = dto.DisplayName
        };
    }

    private static string? ExtractResourceId(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts[^1];
    }

    private sealed class OauthClientListDto
    {
        public List<OauthClientDto>? OauthClients { get; set; }
    }

    private sealed class OauthClientDto
    {
        public string? Name { get; set; }
        public string? ClientId { get; set; }
        public string? DisplayName { get; set; }
    }
}
