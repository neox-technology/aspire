namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// First-party API permission (for example Microsoft Graph) emitted as Graph
/// <c>requiredResourceAccess</c> by <c>WithPermission</c>.
/// </summary>
public sealed record AzureEntraIdWellKnownPermission {

    /// <summary>
    /// Creates a well-known permission.
    /// </summary>
    /// <param name="resourceAppId">Target resource application id (Microsoft Graph is <c>00000003-0000-0000-c000-000000000000</c>).</param>
    /// <param name="id">Graph oauth2 permission scope or app role id.</param>
    /// <param name="type">Graph <c>resourceAccess.type</c>.</param>
    /// <param name="value">Permission value (for example <c>User.Read.All</c>).</param>
    public AzureEntraIdWellKnownPermission(
        string resourceAppId,
        Guid id,
        AzureEntraIdPermissionType type,
        string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceAppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (id == Guid.Empty) {
            throw new ArgumentException("Permission id must be non-empty.", nameof(id));
        }

        if (!Enum.IsDefined(type)) {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        Id = id;
        ResourceAppId = resourceAppId;
        Type = type;
        Value = value;
    }

    /// <summary>
    /// Graph oauth2 permission scope or app role id.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Target resource application id (for example Microsoft Graph <c>00000003-...</c>).
    /// </summary>
    public string ResourceAppId { get; }

    /// <summary>
    /// Graph <c>resourceAccess.type</c>: <see cref="AzureEntraIdPermissionType.Scope"/> or <see cref="AzureEntraIdPermissionType.Role"/>.
    /// </summary>
    public AzureEntraIdPermissionType Type { get; }

    /// <summary>
    /// Permission value (for example <c>User.Read.All</c>).
    /// </summary>
    public string Value { get; }
}
