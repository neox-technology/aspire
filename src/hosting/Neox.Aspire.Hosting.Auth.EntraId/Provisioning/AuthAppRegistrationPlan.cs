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
    UpdateDisplayName
}

/// <summary>
/// Read-only Graph snapshot used for compare.
/// </summary>
public sealed class AuthAppRegistrationExistingSnapshot
{
    public required string ObjectId { get; init; }
    public required string AppId { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>
/// Stores the latest <see cref="AuthAppRegistrationPlan"/> on an <see cref="AuthAppResource"/>.
/// </summary>
public sealed class AuthAppRegistrationPlanAnnotation(AuthAppRegistrationPlan plan) : IResourceAnnotation
{
    public AuthAppRegistrationPlan Plan { get; } = plan
        ?? throw new ArgumentNullException(nameof(plan));
}
