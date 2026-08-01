namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Options for <see cref="AuthProviderExtensions"/> <c>.Entra(...)</c>.
/// </summary>
public sealed class EntraAuthProviderOptions
{
    /// <summary>
    /// Entra tenant id (GUID). When set, becomes the default for the tenant parameter.
    /// </summary>
    public string? TenantId { get; set; }
}
