using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Shared UpdateState helpers for Entra dashboard Auth commands.
/// </summary>
internal static class EntraAuthCommandEnablement
{
    /// <summary>
    /// Returns true when every Auth-app <see cref="WaitAnnotation"/> target is Healthy in the
    /// dashboard status cache (missing cache entry → not ready). No Auth WaitFor deps → true.
    /// </summary>
    public static bool AreWaitForAuthDependenciesHealthy(
        EntraAuthAppRegistrationResource app,
        EntraAuthDashboardStatusService? statusService)
    {
        ArgumentNullException.ThrowIfNull(app);

        foreach (var wait in app.Annotations.OfType<WaitAnnotation>())
        {
            if (wait.Resource is not EntraAuthAppRegistrationResource dependency)
            {
                continue;
            }

            if (statusService is null
                || !statusService.TryGetAppStatus(dependency.Name, out var status)
                || status != AuthDashboardStatus.Healthy)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsClientIdUnsetOrCreateSentinel(
        ParameterResource clientIdParameter,
        IServiceProvider services)
    {
        if (!AuthParameterResolution.TryGetResolvedValue(clientIdParameter, services, out var clientId)
            || string.IsNullOrWhiteSpace(clientId))
        {
            return true;
        }

        return string.Equals(
            clientId,
            EntraAppRegistrationParameterPrompt.CreateSentinel,
            StringComparison.Ordinal);
    }

    public static bool IsExplicitCreateSentinel(
        ParameterResource clientIdParameter,
        IServiceProvider services) =>
        AuthParameterResolution.TryGetResolvedValue(clientIdParameter, services, out var clientId)
        && string.Equals(
            clientId,
            EntraAppRegistrationParameterPrompt.CreateSentinel,
            StringComparison.Ordinal);
}
