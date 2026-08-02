using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Options for <c>.Entra(...)</c> on an AuthOps provider builder.
/// </summary>
public sealed class EntraAuthProviderOptions
{
    /// <summary>
    /// Aspire parameter for the Entra tenant id. When <see langword="null"/>, AuthOps creates
    /// <c>{providerName}-tenant-id</c> with a Choice combobox of tenants the credential can access.
    /// </summary>
    public IResourceBuilder<ParameterResource>? TenantId { get; set; }
}
