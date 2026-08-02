namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Logical AuthOps dashboard status before mapping to Aspire <c>Waiting</c> / <c>Running</c> + health.
/// </summary>
public enum AuthDashboardStatus
{
    /// <summary>Resource is blocked on a dependency or unresolved parameter.</summary>
    Waiting,

    /// <summary>Resource is Running and healthy.</summary>
    Healthy,

    /// <summary>Resource is Running but unhealthy (missing in provider, probe failure, or unhealthy child).</summary>
    Unhealthy
}
