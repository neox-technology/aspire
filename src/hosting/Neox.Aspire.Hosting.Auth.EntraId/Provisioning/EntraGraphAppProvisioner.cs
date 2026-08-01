using Aspire.Hosting.Azure;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Microsoft Graph implementation of <see cref="IEntraGraphAppProvisioner"/>.
/// </summary>
public sealed class EntraGraphAppProvisioner : IEntraGraphAppProvisioner
{
    private const string NeoxMarkerExtension = "neoxAspireAuthApp";

    private readonly GraphServiceClient _graph;
    private readonly IConfiguration? _configuration;

    public EntraGraphAppProvisioner(GraphServiceClient graph, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _configuration = configuration;
    }

    public static EntraGraphAppProvisioner Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetService<IConfiguration>();
        var credential = services.GetService<ITokenCredentialProvider>()?.TokenCredential
            ?? new DefaultAzureCredential();

        var graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        return new EntraGraphAppProvisioner(graph, configuration);
    }

    public async Task<EntraProvisionResult> ProvisionAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Provider is not EntraAuthProviderResource entra)
        {
            throw new InvalidOperationException(
                $"Auth app '{app.Name}' provider '{app.Provider.Name}' is not Entra.");
        }

        var tenantId = await ResolveTenantIdAsync(entra, app, cancellationToken).ConfigureAwait(false);
        var options = app.Options;
        var displayName = options.DisplayName ?? app.Name;

        if (!string.IsNullOrWhiteSpace(options.ExistingClientId))
        {
            return await AdoptAsync(tenantId, options.ExistingClientId!, options, cancellationToken)
                .ConfigureAwait(false);
        }

        var existing = await FindByDisplayNameAsync(displayName, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            await UpdateRedirectsAsync(existing, options, cancellationToken).ConfigureAwait(false);
            string? secret = null;
            if (options.RotateClientSecret || (options.CreateClientSecret && string.IsNullOrEmpty(existing.AppId)))
            {
                // Rotate only when explicitly requested on an existing app.
                if (options.RotateClientSecret && !string.IsNullOrEmpty(existing.Id))
                {
                    secret = await AddPasswordAsync(existing.Id!, cancellationToken).ConfigureAwait(false);
                }
            }

            return new EntraProvisionResult
            {
                TenantId = tenantId,
                ClientId = existing.AppId ?? throw new InvalidOperationException("Graph application missing AppId."),
                ClientSecret = secret,
                ApplicationObjectId = existing.Id
            };
        }

        var created = await CreateApplicationAsync(displayName, options, cancellationToken).ConfigureAwait(false);
        string? createdSecret = null;
        if (options.CreateClientSecret && !string.IsNullOrEmpty(created.Id))
        {
            createdSecret = await AddPasswordAsync(created.Id!, cancellationToken).ConfigureAwait(false);
        }

        return new EntraProvisionResult
        {
            TenantId = tenantId,
            ClientId = created.AppId ?? throw new InvalidOperationException("Created application missing AppId."),
            ClientSecret = createdSecret,
            ApplicationObjectId = created.Id
        };
    }

    private async Task<string> ResolveTenantIdAsync(
        EntraAuthProviderResource entra,
        AuthAppResource app,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(entra.TenantId))
        {
            return entra.TenantId;
        }

        if (app.TenantIdParameter is not null)
        {
            var fromParam = await app.TenantIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fromParam))
            {
                return fromParam;
            }
        }

        var fromConfig = FirstNonEmpty(
            _configuration?["Azure:TenantId"],
            Environment.GetEnvironmentVariable("Azure__TenantId"),
            Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));

        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        throw new InvalidOperationException(
            "Entra tenant id is required. Set Entra options TenantId, Parameters__{provider}-tenant-id, or Azure__TenantId.");
    }

    private async Task<EntraProvisionResult> AdoptAsync(
        string tenantId,
        string clientId,
        AuthAppOptions options,
        CancellationToken cancellationToken)
    {
        Application? app;
        try
        {
            // Filter by appId (client id).
            var page = await _graph.Applications.GetAsync(request =>
            {
                request.QueryParameters.Filter = $"appId eq '{EscapeOData(clientId)}'";
                request.QueryParameters.Top = 1;
            }, cancellationToken).ConfigureAwait(false);

            app = page?.Value?.FirstOrDefault();
        }
        catch (ODataError ex)
        {
            throw new InvalidOperationException(
                $"Failed to look up Entra application '{clientId}': {ex.Error?.Message ?? ex.Message}", ex);
        }

        if (app is null)
        {
            throw new InvalidOperationException(
                $"No Entra application found with client id '{clientId}'.");
        }

        await UpdateRedirectsAsync(app, options, cancellationToken).ConfigureAwait(false);

        string? secret = null;
        if (options.RotateClientSecret && !string.IsNullOrEmpty(app.Id))
        {
            secret = await AddPasswordAsync(app.Id!, cancellationToken).ConfigureAwait(false);
        }

        return new EntraProvisionResult
        {
            TenantId = tenantId,
            ClientId = app.AppId ?? clientId,
            ClientSecret = secret,
            ApplicationObjectId = app.Id
        };
    }

    private async Task<Application?> FindByDisplayNameAsync(string displayName, CancellationToken cancellationToken)
    {
        var page = await _graph.Applications.GetAsync(request =>
        {
            request.QueryParameters.Filter = $"displayName eq '{EscapeOData(displayName)}'";
            request.QueryParameters.Top = 5;
        }, cancellationToken).ConfigureAwait(false);

        return page?.Value?.FirstOrDefault();
    }

    private async Task<Application> CreateApplicationAsync(
        string displayName,
        AuthAppOptions options,
        CancellationToken cancellationToken)
    {
        var application = new Application
        {
            DisplayName = displayName,
            SignInAudience = "AzureADMyOrg"
        };

        ApplyPlatform(application, options);

        if (options.IdentifierUris.Count > 0)
        {
            application.IdentifierUris = [.. options.IdentifierUris];
        }

        // Marker note in description for idempotent discovery (extension attrs need directory setup).
        application.Notes = $"neox-aspire-auth:{NeoxMarkerExtension}";

        var created = await _graph.Applications.PostAsync(application, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return created ?? throw new InvalidOperationException("Graph Applications.PostAsync returned null.");
    }

    private async Task UpdateRedirectsAsync(
        Application existing,
        AuthAppOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(existing.Id) || options.RedirectUris.Count == 0)
        {
            return;
        }

        var patch = new Application();
        ApplyPlatform(patch, options);
        if (options.IdentifierUris.Count > 0)
        {
            patch.IdentifierUris = [.. options.IdentifierUris];
        }

        await _graph.Applications[existing.Id].PatchAsync(patch, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ApplyPlatform(Application application, AuthAppOptions options)
    {
        var uris = options.RedirectUris.ToList();
        switch (options.ApplicationType)
        {
            case AuthApplicationType.Web:
                application.Web = new Microsoft.Graph.Models.WebApplication
                {
                    RedirectUris = uris
                };
                break;
            case AuthApplicationType.Spa:
                application.Spa = new SpaApplication
                {
                    RedirectUris = uris
                };
                break;
            case AuthApplicationType.Native:
                application.PublicClient = new PublicClientApplication
                {
                    RedirectUris = uris
                };
                break;
            case AuthApplicationType.Api:
                // API apps typically use identifier URIs; optional redirects ignored.
                break;
        }
    }

    private async Task<string> AddPasswordAsync(string applicationObjectId, CancellationToken cancellationToken)
    {
        var body = new Microsoft.Graph.Applications.Item.AddPassword.AddPasswordPostRequestBody
        {
            PasswordCredential = new PasswordCredential
            {
                DisplayName = "neox-aspire-auth",
                EndDateTime = DateTimeOffset.UtcNow.AddMonths(12)
            }
        };

        var result = await _graph.Applications[applicationObjectId]
            .AddPassword
            .PostAsync(body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result?.SecretText
            ?? throw new InvalidOperationException("Graph AddPassword returned no secret text.");
    }

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
