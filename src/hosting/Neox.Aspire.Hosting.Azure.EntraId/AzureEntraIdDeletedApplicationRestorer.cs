using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Restores soft-deleted Entra applications whose <c>uniqueName</c> matches an app registration.
/// Graph keeps <c>uniqueName</c> reserved after deletion, so the Bicep extension's create reports
/// success while Graph rejects the add with <c>ObjectAlreadyExists</c>.
/// </summary>
internal sealed class AzureEntraIdDeletedApplicationRestorer {
    private const string GraphDeletedApplications =
        "https://graph.microsoft.com/v1.0/directory/deletedItems/microsoft.graph.application?$select=id,uniqueName";

    public async Task RestoreAsync(
        IServiceProvider services,
        DistributedApplicationModel model,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(model);

        var names = model.Resources
            .OfType<AzureEntraIdAppRegistrationResource>()
            .Select(UniqueName)
            .Where(name => name is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (names.Length == 0) {
            return;
        }

        var credential = services.GetRequiredService<ITokenCredentialProvider>().TokenCredential;
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://graph.microsoft.com/.default"]),
            cancellationToken).ConfigureAwait(false);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var next = GraphDeletedApplications;

        while (!string.IsNullOrEmpty(next)) {
            using var list = await http.GetAsync(next, cancellationToken).ConfigureAwait(false);
            var listBody = await list.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!list.IsSuccessStatusCode) {
                throw new InvalidOperationException(
                    $"Listing soft-deleted Entra applications failed with HTTP {(int)list.StatusCode}.");
            }

            using var document = JsonDocument.Parse(listBody);

            foreach (var item in document.RootElement.GetProperty("value").EnumerateArray()) {
                if (!item.TryGetProperty("uniqueName", out var uniqueNameProperty)) {
                    continue;
                }

                var uniqueName = uniqueNameProperty.GetString();

                if (uniqueName is null || !names.Contains(uniqueName, StringComparer.Ordinal)) {
                    continue;
                }

                var id = item.GetProperty("id").GetString();
                using var content = new StringContent("{}", Encoding.UTF8, "application/json");
                using var restore = await http.PostAsync(
                    $"https://graph.microsoft.com/v1.0/directory/deletedItems/{id}/restore",
                    content,
                    cancellationToken).ConfigureAwait(false);

                if (!restore.IsSuccessStatusCode) {
                    throw new InvalidOperationException(
                        $"Restoring Entra application '{uniqueName}' failed with HTTP {(int)restore.StatusCode}.");
                }
            }

            next = document.RootElement.TryGetProperty("@odata.nextLink", out var nextLink)
                ? nextLink.GetString()
                : null;
        }
    }

    private static string? UniqueName(AzureEntraIdAppRegistrationResource resource) {
        if (resource.Parameters.TryGetValue(AzureEntraIdAppRegistrationResource.UniqueNameParameter, out var value)
            && value is string name
            && !string.IsNullOrEmpty(name)) {
            return name;
        }

        return null;
    }
}
