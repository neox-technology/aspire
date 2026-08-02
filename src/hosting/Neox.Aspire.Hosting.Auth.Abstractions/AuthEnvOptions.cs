namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Options for generic <c>AUTH_*</c> env injection (Google and other non-Identity.Web providers).
/// </summary>
public sealed class AuthEnvOptions
{
    /// <summary>
    /// Env var prefix (default from the registration resource's default prefix).
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// When false, skip emitting <c>{Prefix}_AUTHORITY</c>.
    /// </summary>
    public bool IncludeAuthority { get; set; } = true;

    /// <summary>
    /// When true, emit <c>{Prefix}_REDIRECT_URI</c> (requires a future redirect URI <c>WithXxx</c>).
    /// </summary>
    public bool IncludeRedirectUri { get; set; }

    /// <summary>
    /// When <c>true</c>, emit <c>{Prefix}_CLIENT_SECRET</c>.
    /// When <c>null</c> or <c>false</c> (default), omit the secret until a create-secret <c>WithXxx</c> exists.
    /// </summary>
    public bool? IncludeClientSecret { get; set; }

    private readonly Dictionary<AuthOutput, string> _maps = [];

    /// <summary>
    /// Maps an Auth output to a custom environment variable name (replaces the default <c>{Prefix}_{SETTING}</c>).
    /// </summary>
    public AuthEnvOptions Map(AuthOutput output, string environmentVariableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentVariableName);
        _maps[output] = environmentVariableName;
        return this;
    }

    internal string ResolveName(AuthOutput output, string prefix)
    {
        if (_maps.TryGetValue(output, out var custom))
        {
            return custom;
        }

        var setting = output switch
        {
            AuthOutput.TenantId => "TENANT_ID",
            AuthOutput.ClientId => "CLIENT_ID",
            AuthOutput.ClientSecret => "CLIENT_SECRET",
            AuthOutput.Authority => "AUTHORITY",
            AuthOutput.RedirectUri => "REDIRECT_URI",
            AuthOutput.Instance => "INSTANCE",
            _ => throw new ArgumentOutOfRangeException(nameof(output), output, null)
        };

        return $"{prefix}_{setting}";
    }
}
