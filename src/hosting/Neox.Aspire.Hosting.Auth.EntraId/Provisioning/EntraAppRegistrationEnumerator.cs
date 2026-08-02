using Azure.Core;
using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Enumerates Entra app registrations in a tenant for ClientId Choice options (Microsoft Graph).
/// </summary>
internal static class EntraAppRegistrationEnumerator
{
    public const int MaxOptions = 200;

    /// <summary>
    /// Returns Choice options keyed by application (client) id, labeled <c>DisplayName — appId</c>.
    /// Empty when enumeration fails (missing rights, network, etc.).
    /// </summary>
    public static async Task<IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>>> TryGetAppRegistrationOptionsAsync(
        string tenantId,
        TokenCredential? credential = null,
        GraphServiceClient? graph = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return [];
        }

        try
        {
            graph ??= CreateGraphClient(tenantId, credential);
            var options = new List<System.Collections.Generic.KeyValuePair<string, string>>();

            var page = await graph.Applications.GetAsync(request =>
            {
                request.QueryParameters.Select = ["appId", "displayName"];
                request.QueryParameters.Top = 100;
            }, cancellationToken).ConfigureAwait(false);

            while (page is not null && options.Count < MaxOptions)
            {
                foreach (var app in page.Value ?? [])
                {
                    if (options.Count >= MaxOptions)
                    {
                        break;
                    }

                    var appId = app.AppId;
                    if (string.IsNullOrWhiteSpace(appId))
                    {
                        continue;
                    }

                    var displayName = string.IsNullOrWhiteSpace(app.DisplayName)
                        ? appId
                        : app.DisplayName!;
                    options.Add(System.Collections.Generic.KeyValuePair.Create(appId, $"{displayName} — {appId}"));
                }

                if (string.IsNullOrEmpty(page.OdataNextLink) || options.Count >= MaxOptions)
                {
                    break;
                }

                page = await graph.Applications
                    .WithUrl(page.OdataNextLink)
                    .GetAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            return options
                .OrderBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static GraphServiceClient CreateGraphClient(string tenantId, TokenCredential? credential)
    {
        credential ??= new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = tenantId
        });

        return new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }
}
