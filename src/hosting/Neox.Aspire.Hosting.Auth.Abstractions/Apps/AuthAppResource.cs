using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Logical app registration under an Auth provider.
/// </summary>
public sealed class AuthAppResource : Resource
{
    public AuthAppResource(string name, AuthOpsResourceBase provider, string displayName)
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

    /// <summary>
    /// Desired display name in the identity provider (from <c>AddAppRegistration</c>).
    /// </summary>
    public string DisplayName { get; }

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

            return $"AUTH_{providerToken}_{AuthOpsResourceBase.SanitizeEnvToken(Name)}";
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
