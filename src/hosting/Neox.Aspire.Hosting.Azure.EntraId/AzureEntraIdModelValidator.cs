using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Checks an app registration model before its Graph Bicep module is emitted.
/// </summary>
internal static class AzureEntraIdModelValidator {

    public static void Validate(AzureEntraIdAppRegistrationResource resource) {
        ArgumentNullException.ThrowIfNull(resource);

        var webUris = resource.CollectRedirectUris();
        var spaUris = resource.CollectSpaRedirectUris();

        if (webUris.Intersect(spaUris, StringComparer.Ordinal).Any()) {
            throw new InvalidOperationException(
                $"App registration '{resource.Name}' has the same redirect URI on web and spa platforms.");
        }

        var hasIdentifierUri = resource.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _)
            || (resource.TryGetLastAnnotation<AzureEntraIdIdentifierUrisAnnotation>(out var identifierUris)
                && identifierUris.IdentifierUris.Count > 0);

        if (resource.Scopes.Count > 0 && !hasIdentifierUri) {
            throw new InvalidOperationException(
                $"App registration '{resource.Name}' exposes a scope without an identifier URI.");
        }
    }
}
