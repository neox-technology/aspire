using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Fluent configuration for <see cref="AuthAppRegistrationResource"/> desired registration settings.
/// Redirect URIs are a flat list; Entra Graph platform buckets use typed overloads in EntraId.
/// </summary>
public static class AuthAppRegistrationResourceExtensions
{
    /// <summary>
    /// Adds one or more localhost redirect URIs (<c>http</c> and/or <c>https</c>, optional port and path).
    /// </summary>
    /// <param name="builder">Auth app registration builder.</param>
    /// <param name="port">Optional localhost port (1–65535).</param>
    /// <param name="path">Optional path (leading <c>/</c> normalized).</param>
    /// <param name="scheme">URI scheme(s); defaults to <see cref="LocalhostRedirectScheme.Https"/>.</param>
    public static IResourceBuilder<T> WithLocalhostRedirectUri<T>(
        this IResourceBuilder<T> builder,
        int? port = null,
        string? path = null,
        LocalhostRedirectScheme scheme = LocalhostRedirectScheme.Https)
        where T : AuthAppRegistrationResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (var uri in BuildLocalhostUris(port, path, scheme))
        {
            WithRedirectUri(builder, uri);
        }

        return builder;
    }

    /// <summary>
    /// Adds an absolute redirect URI literal to the Auth app desired state.
    /// </summary>
    public static IResourceBuilder<T> WithRedirectUri<T>(
        this IResourceBuilder<T> builder,
        string uri)
        where T : AuthAppRegistrationResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Redirect URI must be an absolute http or https URI.",
                nameof(uri));
        }

        builder.Resource.AddRedirectUri(AuthRedirectUri.FromLiteral(uri));
        return builder;
    }

    /// <summary>
    /// Adds a redirect URI whose base comes from an Aspire parameter, with an optional path
    /// concatenated at resolution time.
    /// </summary>
    public static IResourceBuilder<T> WithRedirectUri<T>(
        this IResourceBuilder<T> builder,
        IResourceBuilder<ParameterResource> uri,
        string? path = null)
        where T : AuthAppRegistrationResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        builder.Resource.AddRedirectUri(
            AuthRedirectUri.FromParameter(uri.Resource, NormalizePath(path)));
        return builder;
    }

    /// <summary>
    /// Builds localhost redirect URI literals for the given port, path, and scheme.
    /// </summary>
    internal static IReadOnlyList<string> BuildLocalhostUris(
        int? port,
        string? path,
        LocalhostRedirectScheme scheme)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535 when specified.");
        }

        var schemes = scheme switch
        {
            LocalhostRedirectScheme.Https => new[] { Uri.UriSchemeHttps },
            LocalhostRedirectScheme.Http => new[] { Uri.UriSchemeHttp },
            LocalhostRedirectScheme.Both => new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps },
            _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "Unknown localhost redirect scheme.")
        };

        var normalizedPath = NormalizePath(path);
        var uris = new string[schemes.Length];
        for (var i = 0; i < schemes.Length; i++)
        {
            var uri = port is null
                ? $"{schemes[i]}://localhost"
                : $"{schemes[i]}://localhost:{port.Value}";

            if (normalizedPath is not null)
            {
                uri += normalizedPath;
            }

            uris[i] = uri;
        }

        return uris;
    }

    /// <summary>
    /// Returns a path with a single leading <c>/</c>, or <see langword="null"/> when blank.
    /// </summary>
    internal static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
