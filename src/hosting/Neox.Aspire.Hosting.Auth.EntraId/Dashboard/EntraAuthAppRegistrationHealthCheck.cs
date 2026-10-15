using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Aspire AppHost health check for an Entra Auth app registration.
/// Mirrors <see cref="EntraAuthDashboardStatusService"/> probe status so
/// <c>WaitUntilHealthy</c> waits for Running + Healthy (not merely Running).
/// </summary>
internal sealed class EntraAuthAppRegistrationHealthCheck : IHealthCheck
{
    private readonly string _appResourceName;
    private readonly EntraAuthDashboardStatusService _statusService;

    public EntraAuthAppRegistrationHealthCheck(
        string appResourceName,
        EntraAuthDashboardStatusService statusService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appResourceName);
        ArgumentNullException.ThrowIfNull(statusService);

        _appResourceName = appResourceName;
        _statusService = statusService;
    }

    public static string GetKey(string appResourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appResourceName);
        return $"auth-ops:{appResourceName}";
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_statusService.TryGetAppStatus(_appResourceName, out var status))
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Auth app registration status is unknown."));
        }

        if (status == AuthDashboardStatus.Healthy)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy("Auth app registration is healthy."));
        }

        var description = status switch
        {
            AuthDashboardStatus.Waiting => "Auth app registration is waiting.",
            AuthDashboardStatus.Unhealthy => "Auth app registration is unhealthy.",
            _ => "Auth app registration status is unknown."
        };

        return Task.FromResult(HealthCheckResult.Unhealthy(description));
    }
}
