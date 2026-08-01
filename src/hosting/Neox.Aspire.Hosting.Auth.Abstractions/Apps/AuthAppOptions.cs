namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Desired state for an AuthOps app registration.
/// </summary>
public sealed class AuthAppOptions
{
    /// <summary>
    /// Display name in the identity provider. Defaults to the Auth app resource name when unset.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Application platform type.
    /// </summary>
    public AuthApplicationType ApplicationType { get; set; } = AuthApplicationType.Web;

    /// <summary>
    /// Redirect URIs (Web / Spa / Native).
    /// </summary>
    public IList<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// Application ID URIs (typically for API apps).
    /// </summary>
    public IList<string> IdentifierUris { get; set; } = [];

    /// <summary>
    /// When true, create a client secret (password credential) on create.
    /// </summary>
    public bool CreateClientSecret { get; set; } = true;

    /// <summary>
    /// When set, adopt this existing client (application) id instead of creating a new registration.
    /// </summary>
    public string? ExistingClientId { get; set; }

    /// <summary>
    /// When true on adopt/update, create a new password credential (default false — no rotation).
    /// </summary>
    public bool RotateClientSecret { get; set; }
}
