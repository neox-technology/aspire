namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Options for <see cref="AuthOpsExtensions.WithAuth{T}"/> env injection.
/// </summary>
public sealed class AuthEnvOptions
{
    /// <summary>
    /// Env var prefix (default from <see cref="AuthAppResource.DefaultEnvPrefix"/>).
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// When false, skip emitting <c>{Prefix}_AUTHORITY</c>.
    /// </summary>
    public bool IncludeAuthority { get; set; } = true;

    /// <summary>
    /// When true and the app has redirect URIs, emit <c>{Prefix}_REDIRECT_URI</c> (first URI).
    /// </summary>
    public bool IncludeRedirectUri { get; set; }

    /// <summary>
    /// When set, controls whether <c>{Prefix}_CLIENT_SECRET</c> is emitted.
    /// When <c>null</c> (default), follows <see cref="AuthAppOptions.CreateClientSecret"/>.
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
            _ => throw new ArgumentOutOfRangeException(nameof(output), output, null)
        };

        return $"{prefix}_{setting}";
    }
}
