using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Outcome of <c>plan-{app}-auth</c> for Google adopt/bind (no IAM mutate).
/// </summary>
public sealed class GoogleOauthClientPlan
{
    public required GoogleOauthClientPlanMode Mode { get; init; }

    public required string ProjectId { get; init; }

    public required string ClientId { get; init; }

    public string? DesiredDisplayName { get; init; }

    public GoogleOauthClientExistingSnapshot? Existing { get; init; }

    public required IReadOnlyList<GoogleOauthClientPlanAction> Actions { get; init; }

    public bool IsNoOp =>
        Actions.Count == 0
        || (Actions.Count == 1 && Actions[0] == GoogleOauthClientPlanAction.None);
}

public enum GoogleOauthClientPlanMode
{
    Bind
}

public enum GoogleOauthClientPlanAction
{
    None,
    BindClientId
}

/// <summary>
/// Snapshot of an existing IAM oauth client when GET/list validation succeeds.
/// </summary>
public sealed class GoogleOauthClientExistingSnapshot
{
    public required string OauthClientResourceId { get; init; }

    public required string ClientId { get; init; }

    public string? DisplayName { get; init; }
}

internal sealed class GoogleOauthClientPlanAnnotation(GoogleOauthClientPlan plan) : IResourceAnnotation
{
    public GoogleOauthClientPlan Plan { get; } = plan;
}
