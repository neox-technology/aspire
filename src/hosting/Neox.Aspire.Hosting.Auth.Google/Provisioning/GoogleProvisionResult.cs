namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Result of Google IAM oauth client provision.
/// </summary>
public sealed class GoogleProvisionResult
{
    public required string ProjectId { get; init; }

    public required string ClientId { get; init; }

    public string? ClientSecret { get; init; }
}
