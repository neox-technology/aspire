using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Outcome of <c>plan-{app}-auth</c>: desired-vs-existing compare for an Entra app registration.
/// </summary>
public sealed class AuthAppRegistrationPlan
{
    /// <summary>
    /// Whether the target is a new application or an existing one.
    /// </summary>
    public required AuthAppRegistrationPlanMode Mode { get; init; }

    /// <summary>
    /// Resolved Entra tenant id for management and workload binding.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Desired display name from <c>AddAppRegistration</c>.
    /// </summary>
    public required string DesiredDisplayName { get; init; }

    /// <summary>
    /// Desired Graph <c>signInAudience</c> from <c>WithSupportedAccounts</c>
    /// (default <c>AzureADMyOrg</c>).
    /// </summary>
    public string DesiredSignInAudience { get; init; } = SupportedAccountsMapping.DefaultSignInAudience;

    /// <summary>
    /// Resolved redirect URIs from <c>WithRedirectUri</c> / <c>WithLocalhostRedirectUri</c>
    /// (<see cref="AuthApplicationType.Api"/> entries are omitted — Graph has no redirect bucket).
    /// </summary>
    public IReadOnlyList<AuthDesiredRedirectUri> DesiredRedirectUris { get; init; } = [];

    /// <summary>
    /// Desired Application ID URIs. May include the deferred template marker
    /// <see cref="AuthDesiredIdentifierUri.ClientIdTemplateMarker"/> for create path.
    /// </summary>
    public IReadOnlyList<AuthDesiredIdentifierUri> DesiredIdentifierUris { get; init; } = [];

    /// <summary>
    /// Desired OAuth2 permission scopes.
    /// </summary>
    public IReadOnlyList<AuthDesiredOauth2PermissionScope> DesiredScopes { get; init; } = [];

    /// <summary>
    /// Desired app roles.
    /// </summary>
    public IReadOnlyList<AuthDesiredAppRole> DesiredAppRoles { get; init; } = [];

    /// <summary>
    /// Desired required resource access (API permissions).
    /// </summary>
    public IReadOnlyList<AuthDesiredRequiredResourceAccess> DesiredRequiredResourceAccess { get; init; } = [];

    /// <summary>
    /// Snapshot of the existing Graph application when <see cref="Mode"/> is <see cref="AuthAppRegistrationPlanMode.Adopt"/>.
    /// </summary>
    public AuthAppRegistrationExistingSnapshot? Existing { get; init; }

    /// <summary>
    /// Actions <c>provision-{app}-auth</c> must apply.
    /// </summary>
    public required IReadOnlyList<AuthAppRegistrationPlanAction> Actions { get; init; }

    /// <summary>
    /// True when no mutating Graph writes are required.
    /// </summary>
    public bool IsNoOp =>
        Actions.Count == 0
        || (Actions.Count == 1 && Actions[0] == AuthAppRegistrationPlanAction.None);
}

/// <summary>
/// Create vs adopt for an Auth app registration plan.
/// </summary>
public enum AuthAppRegistrationPlanMode
{
    Create,
    Adopt
}

/// <summary>
/// Discrete actions the provision step may apply.
/// </summary>
public enum AuthAppRegistrationPlanAction
{
    None,
    CreateApplication,
    UpdateDisplayName,
    UpdateRedirectUris,
    UpdateSignInAudience,
    UpdateIdentifierUris,
    UpdateOauth2PermissionScopes,
    UpdateAppRoles,
    UpdateRequiredResourceAccess
}

/// <summary>
/// Resolved redirect URI ready for Graph compare / apply.
/// </summary>
public sealed class AuthDesiredRedirectUri
{
    public required AuthApplicationType Type { get; init; }
    public required string Uri { get; init; }
}

/// <summary>
/// Desired Application ID URI.
/// </summary>
public sealed class AuthDesiredIdentifierUri
{
    /// <summary>Sentinel meaning resolve to <c>api://{ClientId}</c> after AppId is known.</summary>
    public const string ClientIdTemplateMarker = "api://{ClientId}";

    public required string Uri { get; init; }

    public bool IsClientIdTemplate =>
        string.Equals(Uri, ClientIdTemplateMarker, StringComparison.Ordinal);
}

/// <summary>
/// Desired OAuth2 permission scope.
/// </summary>
public sealed record AuthDesiredOauth2PermissionScope
{
    public required Guid Id { get; init; }
    public required string Value { get; init; }
    public required string AdminConsentDisplayName { get; init; }
    public required string AdminConsentDescription { get; init; }
    public string? UserConsentDisplayName { get; init; }
    public string? UserConsentDescription { get; init; }
    /// <summary>Graph type: <c>Admin</c> or <c>User</c>.</summary>
    public required string Type { get; init; }
}

/// <summary>
/// Desired app role.
/// </summary>
public sealed record AuthDesiredAppRole
{
    public required Guid Id { get; init; }
    public required string Value { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> AllowedMemberTypes { get; init; }
}

/// <summary>
/// Desired required resource access entry (one resource app + one permission).
/// </summary>
public sealed class AuthDesiredRequiredResourceAccess
{
    /// <summary>Resource (exposer) application ClientId / appId.</summary>
    public required string ResourceAppId { get; init; }

    public required Guid PermissionId { get; init; }

    /// <summary>Graph type: <c>Scope</c> or <c>Role</c>.</summary>
    public required string Type { get; init; }
}

/// <summary>
/// Read-only Graph snapshot used for compare.
/// </summary>
public sealed class AuthAppRegistrationExistingSnapshot
{
    public required string ObjectId { get; init; }
    public required string AppId { get; init; }
    public string? DisplayName { get; init; }

    /// <summary>
    /// Existing Graph <c>signInAudience</c>.
    /// </summary>
    public string? SignInAudience { get; init; }

    /// <summary>
    /// Existing redirect URIs grouped by platform (Web / Spa / Native).
    /// </summary>
    public IReadOnlyList<AuthDesiredRedirectUri> RedirectUris { get; init; } = [];

    public IReadOnlyList<AuthDesiredIdentifierUri> IdentifierUris { get; init; } = [];

    public IReadOnlyList<AuthDesiredOauth2PermissionScope> Scopes { get; init; } = [];

    public IReadOnlyList<AuthDesiredAppRole> AppRoles { get; init; } = [];

    public IReadOnlyList<AuthDesiredRequiredResourceAccess> RequiredResourceAccess { get; init; } = [];
}

/// <summary>
/// Stores the latest <see cref="AuthAppRegistrationPlan"/> on an <see cref="AuthAppResource"/>.
/// </summary>
public sealed class AuthAppRegistrationPlanAnnotation(AuthAppRegistrationPlan plan) : IResourceAnnotation
{
    public AuthAppRegistrationPlan Plan { get; } = plan
        ?? throw new ArgumentNullException(nameof(plan));
}
