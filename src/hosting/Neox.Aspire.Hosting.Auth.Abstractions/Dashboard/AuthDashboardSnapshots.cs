using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Shared <see cref="CustomResourceSnapshot"/> factories for AuthOps dashboard resources.
/// </summary>
internal static class AuthDashboardSnapshots
{
    public static CustomResourceSnapshot Waiting(string resourceType) =>
        new()
        {
            ResourceType = resourceType,
            State = KnownResourceStates.Waiting,
            Properties = []
        };

    public static CustomResourceSnapshot Running(string resourceType) =>
        new()
        {
            ResourceType = resourceType,
            State = KnownResourceStates.Running,
            Properties = []
        };
}
