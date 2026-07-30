using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Abstract Aspire resource representing an OctoDNS DNS <c>provider</c> used by custom domain ops.
/// </summary>
public abstract class DomainOpsProviderResource : Resource
{
    private readonly Dictionary<string, string> _authEnvBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ParameterResource> _authParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _literalSettings = new(StringComparer.Ordinal);

    protected DomainOpsProviderResource(string name)
        : base(name)
    {
    }

    /// <summary>
    /// Fully qualified OctoDNS provider Python class (e.g. <c>octodns_cloudflare.CloudflareProvider</c>).
    /// </summary>
    public abstract string ProviderClass { get; }

    /// <summary>
    /// Default Docker Hub image for <c>octodns-sync</c> (e.g. <c>octodns/cloudflare</c>).
    /// </summary>
    public abstract string DefaultDockerImage { get; }

    /// <summary>
    /// Stable provider slug used in pipeline step names (e.g. <c>cloudflare</c>, <c>ovh</c>).
    /// </summary>
    public abstract string ProviderSlug { get; }

    /// <summary>
    /// YAML provider property → environment variable name (without <c>env/</c> prefix).
    /// </summary>
    public IReadOnlyDictionary<string, string> AuthEnvBindings => _authEnvBindings;

    /// <summary>
    /// YAML provider property → Aspire parameter that supplies the secret at runtime.
    /// </summary>
    public IReadOnlyDictionary<string, ParameterResource> AuthParameters => _authParameters;

    /// <summary>
    /// Non-secret provider settings written as literals in <c>octodns.yaml</c> (e.g. OVH endpoint).
    /// </summary>
    public IReadOnlyDictionary<string, string> LiteralSettings => _literalSettings;

    /// <summary>
    /// Builds the stable env var name for an auth property (e.g. resource <c>dns</c> + <c>token</c> → <c>DNS_TOKEN</c>).
    /// </summary>
    public string GetEnvVarName(string yamlPropertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPropertyName);
        var prefix = SanitizeEnvToken(Name);
        var suffix = SanitizeEnvToken(yamlPropertyName);
        return $"{prefix}_{suffix}";
    }

    /// <summary>
    /// Aspire parameter name for an auth property (e.g. <c>dns-token</c> → <c>Parameters__dns-token</c>).
    /// Names use hyphens so they satisfy Aspire resource naming rules.
    /// </summary>
    public string GetParameterName(string yamlPropertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPropertyName);
        return $"{Name}-{yamlPropertyName.Replace('_', '-')}";
    }

    internal void BindAuthParameter(string yamlPropertyName, ParameterResource parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPropertyName);
        ArgumentNullException.ThrowIfNull(parameter);

        _authParameters[yamlPropertyName] = parameter;
        _authEnvBindings[yamlPropertyName] = GetEnvVarName(yamlPropertyName);
    }

    internal void SetLiteral(string yamlPropertyName, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _literalSettings[yamlPropertyName] = value;
    }

    private static string SanitizeEnvToken(string value)
    {
        var chars = value.Trim()
            .Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }
}
