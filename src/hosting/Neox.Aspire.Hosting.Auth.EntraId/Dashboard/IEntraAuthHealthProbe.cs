namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Read-only Graph probes used by the Entra AuthOps dashboard lifecycle.
/// </summary>
internal interface IEntraAuthHealthProbe
{
    /// <summary>
    /// Looks up an application by client id (appId) and returns existing scope/app role values.
    /// </summary>
    Task<EntraAuthAppProbeResult> ProbeAppAsync(string clientId, CancellationToken cancellationToken);
}
