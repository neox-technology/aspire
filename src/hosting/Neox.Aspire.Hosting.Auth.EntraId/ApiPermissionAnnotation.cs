using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Consumer Auth app dependency on an exposed scope or app role (<c>WithApiPermission</c>).
/// </summary>
public sealed class ApiPermissionAnnotation(ApiPermissionResource permissionResource) : IResourceAnnotation
{
    /// <summary>Dashboard child for this API permission.</summary>
    public ApiPermissionResource PermissionResource { get; } = permissionResource
        ?? throw new ArgumentNullException(nameof(permissionResource));

    /// <summary>Exposed scope or app role to consume.</summary>
    public ApiExposition Exposition =>
        PermissionResource.Exposition
        ?? throw new InvalidOperationException(
            $"API permission '{PermissionResource.Name}' is not bound to an in-model exposition.");
}
