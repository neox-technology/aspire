namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Aggregates AuthOps child statuses with worst-wins ordering.
/// </summary>
public static class AuthStatusAggregator
{
    /// <summary>
    /// Worst-wins: <see cref="AuthDashboardStatus.Unhealthy"/> &gt;
    /// <see cref="AuthDashboardStatus.Waiting"/> &gt;
    /// <see cref="AuthDashboardStatus.Healthy"/>.
    /// Empty sequence returns <paramref name="whenEmpty"/>.
    /// </summary>
    public static AuthDashboardStatus WorstWins(
        IEnumerable<AuthDashboardStatus> statuses,
        AuthDashboardStatus whenEmpty = AuthDashboardStatus.Healthy)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        var sawAny = false;
        var worst = AuthDashboardStatus.Healthy;
        foreach (var status in statuses)
        {
            sawAny = true;
            if (status == AuthDashboardStatus.Unhealthy)
            {
                return AuthDashboardStatus.Unhealthy;
            }

            if (status == AuthDashboardStatus.Waiting)
            {
                worst = AuthDashboardStatus.Waiting;
            }
        }

        return sawAny ? worst : whenEmpty;
    }
}
