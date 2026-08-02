using Aspire.Hosting.Azure;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Microsoft Graph implementation of <see cref="IEntraAuthHealthProbe"/>.
/// </summary>
internal sealed class EntraGraphAuthHealthProbe : IEntraAuthHealthProbe
{
    private readonly GraphServiceClient _graph;

    public EntraGraphAuthHealthProbe(GraphServiceClient graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
    }

    public static EntraGraphAuthHealthProbe Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var credential = services.GetService<ITokenCredentialProvider>()?.TokenCredential
            ?? new DefaultAzureCredential();
        var graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        return new EntraGraphAuthHealthProbe(graph);
    }

    public async Task<EntraAuthAppProbeResult> ProbeAppAsync(string clientId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        try
        {
            var page = await _graph.Applications.GetAsync(request =>
            {
                request.QueryParameters.Filter = $"appId eq '{EscapeOData(clientId)}'";
                request.QueryParameters.Top = 1;
                request.QueryParameters.Select =
                    ["id", "appId", "api", "appRoles", "web", "spa", "publicClient"];
            }, cancellationToken).ConfigureAwait(false);

            var application = page?.Value?.FirstOrDefault();
            if (application is null)
            {
                return EntraAuthAppProbeResult.Missing();
            }

            var scopes = EntraApiExpositionApplicator.ExtractScopes(application)
                .Select(s => s.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var roles = EntraApiExpositionApplicator.ExtractAppRoles(application)
                .Select(r => r.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new EntraAuthAppProbeResult
            {
                Exists = true,
                ObjectId = application.Id,
                ScopeValues = scopes,
                AppRoleValues = roles,
                RedirectUris = EntraRedirectUriApplicator.Extract(application)
            };
        }
        catch (ODataError ex)
        {
            return EntraAuthAppProbeResult.Failed(ex.Error?.Message ?? ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EntraAuthAppProbeResult.Failed(ex.Message);
        }
    }

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
