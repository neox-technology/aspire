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
    /// Adds a localhost redirect URI (<c>https://localhost</c>, optional port and path).
    /// </summary>
    public static IResourceBuilder<T> WithLocalhostRedirectUri<T>(
        this IResourceBuilder<T> builder,
        int? port = null,
        string? path = null)
        where T : AuthAppRegistrationResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535 when specified.");
        }

        var uri = port is null
            ? "https://localhost"
            : $"https://localhost:{port.Value}";

        var normalizedPath = NormalizePath(path);
        if (normalizedPath is not null)
        {
            uri += normalizedPath;
        }

        return WithRedirectUri(builder, uri);
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
