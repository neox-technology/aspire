using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra ID AuthOps provider resource created by <c>.Entra(...)</c>.
/// </summary>
public sealed class EntraAuthOpsResource : AuthOpsResourceBase
{
    public EntraAuthOpsResource(string name)
        : base(name)
    {
    }

    /// <inheritdoc />
    public override string ProviderSlug => "entra";

    /// <summary>
    /// Aspire parameter that supplies the tenant id (<c>{provider}-tenant-id</c>).
    /// </summary>
    public ParameterResource TenantIdParameter { get; internal set; } = null!;
}
