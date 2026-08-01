namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Named workload outputs that <see cref="AuthOpsExtensions.WithAuth{T}"/> can map to custom env var names.
/// </summary>
public enum AuthOutput
{
    TenantId,
    ClientId,
    ClientSecret,
    Authority,
    RedirectUri
}
