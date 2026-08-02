namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Result of creating or adopting an Entra app registration.
/// </summary>
public sealed class EntraProvisionResult
{
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }

    /// <summary>
    /// New client secret when created/rotated; null when adopt without rotation.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Graph object id of the application (not the client/app id).
    /// </summary>
    public string? ApplicationObjectId { get; init; }
}
