namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Exposed Entra app role (Graph <c>appRoles</c>).
/// </summary>
public sealed class AppRoleApiExposition : ApiExposition
{
    public AppRoleApiExposition(
        string name,
        EntraAuthAppRegistrationResource owner,
        AllowedMemberType allowedMemberType,
        string value,
        string description,
        Guid roleId)
        : base(name, owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        AllowedMemberType = allowedMemberType;
        Value = value;
        Description = description;
        RoleId = roleId == Guid.Empty ? Guid.NewGuid() : roleId;
    }

    /// <summary>Who may be assigned this role.</summary>
    public AllowedMemberType AllowedMemberType { get; }

    /// <summary>Graph app role <c>value</c> (claim value).</summary>
    public string Value { get; }

    /// <summary>App role description (also used as display name).</summary>
    public string Description { get; }

    /// <summary>Stable Graph app role id used on create and in <c>requiredResourceAccess</c>.</summary>
    public Guid RoleId { get; }
}
