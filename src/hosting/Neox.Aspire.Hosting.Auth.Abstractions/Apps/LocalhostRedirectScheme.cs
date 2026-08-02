namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// URI scheme(s) for <c>WithLocalhostRedirectUri</c>.
/// </summary>
public enum LocalhostRedirectScheme
{
    /// <summary>
    /// <c>https://localhost</c> only (default).
    /// </summary>
    Https,

    /// <summary>
    /// <c>http://localhost</c> only.
    /// </summary>
    Http,

    /// <summary>
    /// Both <c>http://localhost</c> and <c>https://localhost</c> for the same port/path.
    /// </summary>
    Both
}
