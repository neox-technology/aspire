namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Named workload outputs that provider <c>WithAuth</c> can map to custom env var names.
/// </summary>
public enum AuthOutput
{
    TenantId,
    ClientId,
    ClientSecret,
    Authority,
    RedirectUri,
    /// <summary>Entra Identity.Web instance URL (<c>AzureAd__Instance</c>).</summary>
    Instance
}
