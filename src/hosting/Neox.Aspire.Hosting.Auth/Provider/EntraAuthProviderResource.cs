using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra ID AuthOps provider resource.
/// </summary>
public sealed class EntraAuthProviderResource : AuthProviderResource
{
    public EntraAuthProviderResource(string name)
        : base(name)
    {
    }

    /// <inheritdoc />
    public override string ProviderSlug => "entra";

    /// <summary>
    /// Optional tenant id configured at model build time (may also come from a parameter).
    /// </summary>
    public string? TenantId { get; internal set; }

    /// <summary>
    /// Aspire parameter that supplies the tenant id (GetOrAdd <c>{provider}-tenant-id</c>).
    /// </summary>
    public ParameterResource? TenantIdParameter { get; internal set; }
}
