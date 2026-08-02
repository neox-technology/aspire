using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Abstract non-container Aspire resource representing an identity <c>provider</c> used by AuthOps.
/// </summary>
public abstract class AuthOpsResourceBase : Resource, IResourceWithParent<AuthOpsResource>
{
    private readonly List<AuthAppResource> _apps = [];
    private readonly AuthOpsResource _authOpsParent;

    protected AuthOpsResourceBase(string name, AuthOpsResource authOpsParent)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(authOpsParent);
        _authOpsParent = authOpsParent;
    }

    /// <summary>
    /// Shared AuthOps marker resource (<c>auth-ops</c>) that owns this provider in the dashboard hierarchy.
    /// </summary>
    public AuthOpsResource Parent => _authOpsParent;

    IResource IResourceWithParent.Parent => Parent;

    /// <summary>
    /// Stable provider slug used in <c>AUTH_{SLUG}_*</c> env vars (e.g. <c>entra</c>).
    /// Pipeline prereq steps use the Aspire resource <see cref="Resource.Name"/> instead.
    /// </summary>
    public abstract string ProviderSlug { get; }

    /// <summary>
    /// Optional factory that builds a deferred authority URL from the tenant parameter for <see cref="AuthOpsExtensions.WithAuth{T}"/>.
    /// </summary>
    public Func<ParameterResource, ReferenceExpression>? AuthorityExpression { get; set; }

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
