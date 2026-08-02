namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Who may be assigned an Entra app role (Graph <c>appRoles.allowedMemberTypes</c>).
/// </summary>
public enum AllowedMemberType
{
    /// <summary>Users and groups (<c>User</c>).</summary>
    UsersAndGroups,

    /// <summary>Applications only (<c>Application</c>).</summary>
    Applications,

    /// <summary>Users/groups and applications.</summary>
    Both
}
