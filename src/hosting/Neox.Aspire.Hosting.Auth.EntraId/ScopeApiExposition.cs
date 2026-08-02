namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Exposed OAuth2 permission scope (Graph <c>api.oauth2PermissionScopes</c>).
/// </summary>
public sealed class ScopeApiExposition : ApiExposition
{
    public ScopeApiExposition(
        string name,
        EntraAuthAppRegistrationResource owner,
        string scopeValue,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName,
        string? userConsentDescription,
        bool allowUserConsent,
        Guid permissionId)
        : base(name, owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConsentDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConsentDescription);

        ScopeValue = scopeValue;
        AdminConsentDisplayName = adminConsentDisplayName;
        AdminConsentDescription = adminConsentDescription;
        UserConsentDisplayName = userConsentDisplayName;
        UserConsentDescription = userConsentDescription;
        AllowUserConsent = allowUserConsent;
        PermissionId = permissionId == Guid.Empty ? Guid.NewGuid() : permissionId;
    }

    /// <summary>Graph scope <c>value</c> (e.g. <c>access_as_user</c>).</summary>
    public string ScopeValue { get; }

    /// <summary>Admin consent display name.</summary>
    public string AdminConsentDisplayName { get; }

    /// <summary>Admin consent description.</summary>
    public string AdminConsentDescription { get; }

    /// <summary>Optional user consent display name.</summary>
    public string? UserConsentDisplayName { get; }

    /// <summary>Optional user consent description.</summary>
    public string? UserConsentDescription { get; }

    /// <summary>
    /// When <see langword="true"/>, Graph scope type is <c>User</c> (admins and users can consent);
    /// otherwise <c>Admin</c>.
    /// </summary>
    public bool AllowUserConsent { get; }

    /// <summary>Stable Graph permission id used on create and in <c>requiredResourceAccess</c>.</summary>
    public Guid PermissionId { get; }
}
