using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Graph <c>requiredResourceAccess[].resourceAccess[].type</c>: delegated scope
/// (<see cref="Scope"/>) or app role (<see cref="Role"/>).
/// </summary>
public enum AzureEntraIdPermissionType
{
    /// <summary>
    /// Delegated permission (Graph <c>Scope</c>).
    /// </summary>
    Scope,

    /// <summary>
    /// Application permission (Graph <c>Role</c>).
    /// </summary>
    Role
}

/// <summary>
/// An OAuth2 permission scope or app role that a consumer app registration can
/// request via <c>WithPermission</c> (Graph <c>requiredResourceAccess</c>).
/// </summary>
public interface IAzureEntraIdPermissibleResource : IResourceWithParent<AzureEntraIdAppRegistrationResource>
{
    /// <summary>
    /// Stable Graph permission <c>id</c> (scope or app role).
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Graph <c>resourceAccess[].type</c>: <see cref="AzureEntraIdPermissionType.Scope"/>
    /// or <see cref="AzureEntraIdPermissionType.Role"/>.
    /// </summary>
    AzureEntraIdPermissionType Type { get; }
}
