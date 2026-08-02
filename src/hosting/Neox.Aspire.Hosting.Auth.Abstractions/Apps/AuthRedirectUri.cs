using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Desired redirect URI on an <see cref="AuthAppResource"/> — either a literal absolute URI
/// or a parameter (optional path concatenated at resolution time).
/// </summary>
public sealed class AuthRedirectUri
{
    private AuthRedirectUri(
        AuthApplicationType redirectUriType,
        string? literal,
        ParameterResource? parameter,
        string? path)
    {
        RedirectUriType = redirectUriType;
        Literal = literal;
        Parameter = parameter;
        Path = path;
    }

    /// <summary>
    /// OAuth platform bucket for this URI (<see cref="AuthApplicationType.Web"/>,
    /// <see cref="AuthApplicationType.Spa"/>, etc.).
    /// </summary>
    public AuthApplicationType RedirectUriType { get; }

    /// <summary>
    /// Absolute redirect URI when configured via <c>WithRedirectUri</c> (literal) or
    /// <c>WithLocalhostRedirectUri</c>; otherwise <see langword="null"/>.
    /// </summary>
    public string? Literal { get; }

    /// <summary>
    /// Base URI parameter when configured via <c>WithRedirectUri(parameter, path?)</c>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public ParameterResource? Parameter { get; }

    /// <summary>
    /// Optional path segment appended to <see cref="Parameter"/> at resolution
    /// (normalized with a leading <c>/</c>). <see langword="null"/> for literals.
    /// </summary>
    public string? Path { get; }

    internal static AuthRedirectUri FromLiteral(AuthApplicationType redirectUriType, string uri) =>
        new(redirectUriType, uri, parameter: null, path: null);

    internal static AuthRedirectUri FromParameter(
        AuthApplicationType redirectUriType,
        ParameterResource parameter,
        string? path) =>
        new(redirectUriType, literal: null, parameter, path);
}
