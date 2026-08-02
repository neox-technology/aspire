using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Consumer Auth app dependency on a first-party well-known API permission (<c>WithApiPermission</c>).
/// </summary>
public sealed class WellKnownApiPermissionAnnotation(ApiPermissionResource permissionResource) : IResourceAnnotation
{
    /// <summary>Dashboard child for this API permission.</summary>
    public ApiPermissionResource PermissionResource { get; } = permissionResource
        ?? throw new ArgumentNullException(nameof(permissionResource));

    /// <summary>Well-known permission to include in <c>requiredResourceAccess</c>.</summary>
    public WellKnownApiPermission Permission =>
        PermissionResource.WellKnownPermission
        ?? throw new InvalidOperationException(
            $"API permission '{PermissionResource.Name}' is not bound to a well-known permission.");
}
