namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// First-party API permission (e.g. Microsoft Graph) for <c>requiredResourceAccess</c>,
/// typically emitted by the EntraId Graph permissions source generator.
/// </summary>
public sealed class WellKnownApiPermission
{
    public WellKnownApiPermission(
        string resourceAppId,
        Guid permissionId,
        string type,
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission id must be non-empty.", nameof(permissionId));
        }

        if (!string.Equals(type, "Scope", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(type, "Role", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Type must be \"Scope\" or \"Role\".",
                nameof(type));
        }

        ResourceAppId = resourceAppId;
        PermissionId = permissionId;
        Type = string.Equals(type, "Scope", StringComparison.OrdinalIgnoreCase) ? "Scope" : "Role";
        Value = value;
    }

    /// <summary>Target resource application id (e.g. Microsoft Graph <c>00000003-...</c>).</summary>
    public string ResourceAppId { get; }

    /// <summary>Graph oauth2PermissionScope or appRole id.</summary>
    public Guid PermissionId { get; }

    /// <summary>Graph <c>resourceAccess.type</c>: <c>Scope</c> or <c>Role</c>.</summary>
    public string Type { get; }

    /// <summary>Permission value (e.g. <c>User.Read</c>).</summary>
    public string Value { get; }
}
