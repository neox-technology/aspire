using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Resolve and apply AuthOps redirect URIs onto Microsoft Graph <see cref="Application"/> models.
/// </summary>
internal static class EntraRedirectUriApplicator
{
    /// <summary>
    /// Resolves literal and parameter-based redirect URIs. Skips <see cref="AuthApplicationType.Api"/>.
    /// Prefers Entra typed annotation entries; falls back to flat redirects as <see cref="AuthApplicationType.Web"/>.
    /// </summary>
    public static async Task<IReadOnlyList<AuthDesiredRedirectUri>> ResolveAsync(
        AuthAppResource app,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        var typed = app.Annotations.OfType<EntraRedirectUrisAnnotation>().FirstOrDefault();
        if (typed is not null && typed.Entries.Count > 0)
        {
            var result = new List<AuthDesiredRedirectUri>();
            foreach (var entry in typed.Entries)
            {
                if (entry.Type == AuthApplicationType.Api)
                {
                    continue;
                }

                var uri = await ResolveUriAsync(app, entry.Uri, cancellationToken).ConfigureAwait(false);
                result.Add(new AuthDesiredRedirectUri
                {
                    Type = entry.Type,
                    Uri = uri
                });
            }

            return result;
        }

        var flat = new List<AuthDesiredRedirectUri>();
        foreach (var entry in app.RedirectUris)
        {
            var uri = await ResolveUriAsync(app, entry, cancellationToken).ConfigureAwait(false);
            flat.Add(new AuthDesiredRedirectUri
            {
                Type = AuthApplicationType.Web,
                Uri = uri
            });
        }

        return flat;
    }

    /// <summary>
    /// Reads Web / Spa / PublicClient redirect URIs from a Graph application.
    /// </summary>
    public static IReadOnlyList<AuthDesiredRedirectUri> Extract(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var result = new List<AuthDesiredRedirectUri>();
        Append(result, AuthApplicationType.Web, application.Web?.RedirectUris);
        Append(result, AuthApplicationType.Spa, application.Spa?.RedirectUris);
        Append(result, AuthApplicationType.Native, application.PublicClient?.RedirectUris);
        return result;
    }

    /// <summary>
    /// Sets Web / Spa / PublicClient redirect URI collections from desired state
    /// (empty list clears that platform's redirects).
    /// </summary>
    public static void Apply(Application application, IReadOnlyList<AuthDesiredRedirectUri> desired)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(desired);

        application.Web = new Microsoft.Graph.Models.WebApplication
        {
            RedirectUris = UrisFor(desired, AuthApplicationType.Web)
        };
        application.Spa = new SpaApplication
        {
            RedirectUris = UrisFor(desired, AuthApplicationType.Spa)
        };
        application.PublicClient = new PublicClientApplication
        {
            RedirectUris = UrisFor(desired, AuthApplicationType.Native)
        };
    }

    /// <summary>
    /// Order-insensitive compare of redirect URI sets per platform.
    /// </summary>
    public static bool Differ(
        IReadOnlyList<AuthDesiredRedirectUri> desired,
        IReadOnlyList<AuthDesiredRedirectUri> existing)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(existing);

        foreach (var type in new[]
                 {
                     AuthApplicationType.Web,
                     AuthApplicationType.Spa,
                     AuthApplicationType.Native
                 })
        {
            var left = new HashSet<string>(UrisFor(desired, type), StringComparer.Ordinal);
            var right = new HashSet<string>(UrisFor(existing, type), StringComparer.Ordinal);
            if (!left.SetEquals(right))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ResolveUriAsync(
        AuthAppResource app,
        AuthRedirectUri entry,
        CancellationToken cancellationToken)
    {
        if (entry.Literal is not null)
        {
            return entry.Literal;
        }

        if (entry.Parameter is not null)
        {
            var baseValue = await entry.Parameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(baseValue))
            {
                throw new InvalidOperationException(
                    $"Auth app '{app.Name}' redirect URI parameter '{entry.Parameter.Name}' has no value.");
            }

            return entry.Path is null
                ? baseValue
                : baseValue.TrimEnd('/') + entry.Path;
        }

        throw new InvalidOperationException(
            $"Auth app '{app.Name}' has a redirect URI entry with neither literal nor parameter.");
    }

    private static void Append(
        List<AuthDesiredRedirectUri> target,
        AuthApplicationType type,
        IList<string>? uris)
    {
        if (uris is null)
        {
            return;
        }

        foreach (var uri in uris)
        {
            if (!string.IsNullOrWhiteSpace(uri))
            {
                target.Add(new AuthDesiredRedirectUri { Type = type, Uri = uri });
            }
        }
    }

    private static List<string> UrisFor(
        IReadOnlyList<AuthDesiredRedirectUri> source,
        AuthApplicationType type) =>
        source.Where(u => u.Type == type)
            .Select(u => u.Uri)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
