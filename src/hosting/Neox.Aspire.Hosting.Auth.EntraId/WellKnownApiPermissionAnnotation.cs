using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Consumer Auth app dependency on a first-party well-known API permission (<c>WithApiPermission</c>).
/// </summary>
public sealed class WellKnownApiPermissionAnnotation(WellKnownApiPermission permission) : IResourceAnnotation
{
    /// <summary>Well-known permission to include in <c>requiredResourceAccess</c>.</summary>
    public WellKnownApiPermission Permission { get; } = permission
        ?? throw new ArgumentNullException(nameof(permission));
}
