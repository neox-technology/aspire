using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Desired redirect URI on an <see cref="AuthAppResource"/> — either a literal absolute URI
/// or a parameter (optional path concatenated at resolution time). Platform buckets (Web/Spa/Native)
/// are Entra-specific and live on typed overloads in the EntraId package.
/// </summary>
public sealed class AuthRedirectUri
{
    private AuthRedirectUri(
        string? literal,
        ParameterResource? parameter,
        string? path)
    {
        Literal = literal;
        Parameter = parameter;
        Path = path;
    }

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

    internal static AuthRedirectUri FromLiteral(string uri) =>
        new(uri, parameter: null, path: null);

    internal static AuthRedirectUri FromParameter(
        ParameterResource parameter,
        string? path) =>
        new(literal: null, parameter, path);
}
