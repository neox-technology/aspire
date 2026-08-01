using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Logical app registration under an Auth provider.
/// </summary>
public sealed class AuthAppResource : Resource
{
    public AuthAppResource(string name, AuthProviderResource provider)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Provider = provider;
    }

    /// <summary>
    /// Parent Auth provider.
    /// </summary>
    public AuthProviderResource Provider { get; }

    /// <summary>
    /// Desired OAuth application options.
    /// </summary>
    public AuthAppOptions Options { get; internal set; } = new();

    /// <summary>
    /// Workload tenant id parameter (<c>{provider}-tenant-id</c> shared, or app-qualified when needed).
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

    /// <summary>
    /// Default env prefix for a single app: <c>AUTH_{PROVIDER}</c>; for multiple apps: <c>AUTH_{PROVIDER}_{APP}</c>.
    /// </summary>
    public string DefaultEnvPrefix
    {
        get
        {
            var providerToken = Provider.ProviderEnvToken;
            if (Provider.Apps.Count <= 1)
            {
                return $"AUTH_{providerToken}";
            }

            return $"AUTH_{providerToken}_{AuthProviderResource.SanitizeEnvToken(Name)}";
        }
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
