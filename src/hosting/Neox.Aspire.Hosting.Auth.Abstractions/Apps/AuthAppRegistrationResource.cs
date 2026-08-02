using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Abstract logical app registration under an Auth provider. Concrete types are provider-owned
/// (<c>EntraAuthAppRegistrationResource</c>, <c>GoogleAuthAppRegistrationResource</c>).
/// </summary>
public abstract class AuthAppRegistrationResource : Resource, IResourceWithParent<AuthOpsResourceBase>
{
    protected AuthAppRegistrationResource(string name, AuthOpsResourceBase provider, string displayName)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Provider = provider;
        DisplayName = displayName;
    }

    /// <summary>
    /// Parent Auth provider.
    /// </summary>
    public AuthOpsResourceBase Provider { get; }

    /// <inheritdoc />
    public AuthOpsResourceBase Parent => Provider;

    IResource IResourceWithParent.Parent => Provider;

    /// <summary>
    /// Desired display name in the identity provider (from <c>AddAppRegistration</c>).
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Workload tenant id parameter (<c>{provider}-tenant-id</c> shared, or app-qualified when needed).
    /// For Google this holds the ProjectId.
    /// </summary>
    public ParameterResource TenantIdParameter { get; internal set; } = null!;

    /// <summary>
    /// Workload client id parameter (<c>{provider}-{app}-client-id</c>).
    /// </summary>
    public ParameterResource ClientIdParameter { get; internal set; } = null!;

    /// <summary>
    /// Workload client secret parameter (<c>{provider}-{app}-client-secret</c>, <c>secret: true</c>).
    /// </summary>
    public ParameterResource ClientSecretParameter { get; internal set; } = null!;

    private readonly List<AuthRedirectUri> _redirectUris = [];

    /// <summary>
    /// Desired redirect URIs accumulated via <c>WithRedirectUri</c> / <c>WithLocalhostRedirectUri</c>
    /// (flat list; Entra platform buckets use typed overloads in the EntraId package).
    /// </summary>
    public IReadOnlyList<AuthRedirectUri> RedirectUris => _redirectUris;

    internal void AddRedirectUri(AuthRedirectUri redirectUri)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);
        _redirectUris.Add(redirectUri);
    }

    /// <summary>
    /// Aspire parameter name for a workload setting (dash convention).
    /// </summary>
    public string GetParameterName(string setting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setting);
        var slug = setting.Replace('_', '-');
        return $"{Provider.Name}-{Name}-{slug}";
    }
}
