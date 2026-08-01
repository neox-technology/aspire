using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Non-container Aspire resource representing an identity <c>provider</c> used by AuthOps.
/// </summary>
public abstract class AuthProviderResource : Resource
{
    private readonly List<AuthAppResource> _apps = [];

    protected AuthProviderResource(string name)
        : base(name)
    {
    }

    /// <summary>
    /// Stable provider slug used in pipeline step names and <c>AUTH_{SLUG}_*</c> env vars (e.g. <c>entra</c>).
    /// </summary>
    public abstract string ProviderSlug { get; }

    /// <summary>
    /// Optional formatter that builds an authority URL from a tenant id for <see cref="AuthOpsExtensions.WithAuth{T}"/>.
    /// </summary>
    public Func<string, string>? AuthorityFormatter { get; set; }

    /// <summary>
    /// Auth apps registered under this provider.
    /// </summary>
    public IReadOnlyList<AuthAppResource> Apps => _apps;

    internal void RegisterApp(AuthAppResource app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _apps.Add(app);
    }

    /// <summary>
    /// Uppercase env token for the provider slug (e.g. <c>ENTRA</c>).
    /// </summary>
    public string ProviderEnvToken => SanitizeEnvToken(ProviderSlug);

    internal static string SanitizeEnvToken(string value)
    {
        var chars = value.Trim()
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }
}
