namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Graph <c>appRoles[].allowedMemberTypes</c>: users/groups (<see cref="User"/>)
/// and/or other applications (<see cref="Application"/>).
/// </summary>
[Flags]
public enum AllowedMemberTypes
{
    /// <summary>
    /// Assignable to users and groups (Graph <c>User</c>).
    /// </summary>
    User = 1,

    /// <summary>
    /// Assignable to other applications (Graph <c>Application</c>).
    /// </summary>
    Application = 2
}
