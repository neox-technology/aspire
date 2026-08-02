using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Fluent configuration for <see cref="AuthAppResource"/> desired registration settings.
/// </summary>
public static class AuthAppResourceExtensions
{
    /// <summary>
    /// Adds a localhost redirect URI (<c>https://localhost</c>, optional port and path).
    /// </summary>
    /// <param name="builder">Auth app builder.</param>
    /// <param name="redirectUriType">OAuth platform bucket (Web, Spa, Native, Api).</param>
    /// <param name="port">Optional HTTPS port (e.g. <c>7281</c>).</param>
    /// <param name="path">Optional path (normalized with a leading <c>/</c> when non-blank).</param>
    public static IResourceBuilder<AuthAppResource> WithLocalhostRedirectUri(
        this IResourceBuilder<AuthAppResource> builder,
        AuthApplicationType redirectUriType,
        int? port = null,
        string? path = null)
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

        return WithRedirectUri(builder, redirectUriType, uri);
    }

    /// <summary>
    /// Adds an absolute redirect URI literal to the Auth app desired state.
    /// </summary>
    public static IResourceBuilder<AuthAppResource> WithRedirectUri(
        this IResourceBuilder<AuthAppResource> builder,
        AuthApplicationType redirectUriType,
        string uri)
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

        builder.Resource.AddRedirectUri(AuthRedirectUri.FromLiteral(redirectUriType, uri));
        return builder;
    }

    /// <summary>
    /// Adds a redirect URI whose base comes from an Aspire parameter, with an optional path
    /// concatenated at resolution time.
    /// </summary>
    public static IResourceBuilder<AuthAppResource> WithRedirectUri(
        this IResourceBuilder<AuthAppResource> builder,
        AuthApplicationType redirectUriType,
        IResourceBuilder<ParameterResource> uri,
        string? path = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(uri);

        builder.Resource.AddRedirectUri(
            AuthRedirectUri.FromParameter(redirectUriType, uri.Resource, NormalizePath(path)));
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
