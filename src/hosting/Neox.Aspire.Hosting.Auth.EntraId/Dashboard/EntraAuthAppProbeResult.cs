namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Read-only Graph probe result for an Entra app registration and its expositions.
/// </summary>
internal sealed class EntraAuthAppProbeResult
{
    public required bool Exists { get; init; }

    public string? Error { get; init; }

    /// <summary>Graph application object id when <see cref="Exists"/> is true.</summary>
    public string? ObjectId { get; init; }

    public IReadOnlySet<string> ScopeValues { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> AppRoleValues { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Redirect URIs extracted from Graph Web / Spa / PublicClient when the app exists;
    /// empty otherwise.
    /// </summary>
    public IReadOnlyList<AuthDesiredRedirectUri> RedirectUris { get; init; } = [];

    public static EntraAuthAppProbeResult Missing() => new() { Exists = false };

    public static EntraAuthAppProbeResult Failed(string error) =>
        new() { Exists = false, Error = error };
}
