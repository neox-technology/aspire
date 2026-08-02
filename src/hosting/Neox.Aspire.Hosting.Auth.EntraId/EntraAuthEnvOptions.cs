namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Options for Entra <c>WithAuth</c> env injection (Microsoft.Identity.Web <c>AzureAd__*</c>).
/// </summary>
public sealed class EntraAuthEnvOptions
{
    /// <summary>
    /// Configuration section name (default <c>AzureAd</c> → <c>AzureAd__TenantId</c>, …).
    /// </summary>
    public string Section { get; set; } = "AzureAd";

    /// <summary>
    /// When true (default), emit <c>{Section}__Instance</c> = <c>https://login.microsoftonline.com/</c>.
    /// </summary>
    public bool IncludeInstance { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, emit <c>{Section}__ClientSecret</c>.
    /// When <c>null</c> (default), emit only if the Auth app has <c>WithClientSecret</c>.
    /// When <c>false</c>, suppress emit even if <c>WithClientSecret</c> is present.
    /// </summary>
    public bool? IncludeClientSecret { get; set; }

    /// <summary>
    /// Default Identity.Web instance URL.
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    private readonly Dictionary<AuthOutput, string> _maps = [];

    /// <summary>
    /// Maps an Auth output to a custom environment variable name
    /// (replaces the default <c>{Section}__{Setting}</c>).
    /// </summary>
    public EntraAuthEnvOptions Map(AuthOutput output, string environmentVariableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);
        _maps[output] = environmentVariableName;
        return this;
    }

    internal string ResolveName(AuthOutput output)
    {
        if (_maps.TryGetValue(output, out var custom))
        {
            return custom;
        }

        var setting = output switch
        {
            AuthOutput.TenantId => "TenantId",
            AuthOutput.ClientId => "ClientId",
            AuthOutput.ClientSecret => "ClientSecret",
            AuthOutput.Instance => "Instance",
            AuthOutput.Authority => "Authority",
            AuthOutput.RedirectUri => "RedirectUri",
            _ => throw new ArgumentOutOfRangeException(nameof(output), output, null)
        };

        return $"{Section}__{setting}";
    }
}
