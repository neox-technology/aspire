using System.Collections.Immutable;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Publishes AuthOps dashboard status onto Aspire resource snapshots.
/// </summary>
public static class AuthDashboardStatusPublisher
{
    public const string HealthReportName = "auth-ops";

    /// <summary>
    /// Maps <see cref="AuthDashboardStatus"/> to Aspire <c>Waiting</c> or <c>Running</c> + health reports.
    /// </summary>
    public static async Task PublishAsync(
        ResourceNotificationService notificationService,
        IResource resource,
        AuthDashboardStatus status,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(resource);

        await notificationService.PublishUpdateAsync(
                resource,
                snapshot => Apply(snapshot, status, description, resource))
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static CustomResourceSnapshot Apply(
        CustomResourceSnapshot snapshot,
        AuthDashboardStatus status,
        string? description,
        IResource? resource = null)
    {
        var reportName = ResolveHealthReportName(resource);
        return status switch
        {
            AuthDashboardStatus.Waiting => (snapshot with
            {
                State = KnownResourceStates.Waiting
            }).WithHealthReports([]),
            AuthDashboardStatus.Healthy => WithRunningHealth(
                snapshot,
                HealthStatus.Healthy,
                description ?? "Healthy",
                reportName),
            AuthDashboardStatus.Unhealthy => WithRunningHealth(
                snapshot,
                HealthStatus.Unhealthy,
                description ?? "Unhealthy",
                reportName),
            _ => snapshot
        };
    }

    /// <summary>
    /// Prefer the resource's own <see cref="HealthCheckAnnotation"/> key so we update the same
    /// report Aspire's <c>ResourceHealthCheckService</c> merges (avoids dual Healthy/Unhealthy reports).
    /// </summary>
    internal static string ResolveHealthReportName(IResource? resource)
    {
        var key = resource?.Annotations.OfType<HealthCheckAnnotation>().FirstOrDefault()?.Key;
        return string.IsNullOrWhiteSpace(key) ? HealthReportName : key;
    }

    private static CustomResourceSnapshot WithRunningHealth(
        CustomResourceSnapshot snapshot,
        HealthStatus healthStatus,
        string description,
        string reportName)
    {
        var running = snapshot with
        {
            State = KnownResourceStates.Running,
            StartTimeStamp = snapshot.StartTimeStamp ?? DateTime.UtcNow
        };

        return running.WithHealthReports(ImmutableArray.Create(
            new HealthReportSnapshot(
                reportName,
                healthStatus,
                description,
                null)));
    }
}
