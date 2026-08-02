using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Fluent builder for scopes under <c>WithApiExposition</c>.
/// </summary>
public interface IApiExpositionBuilder
{
    /// <summary>
    /// Adds an OAuth2 permission scope that requires admin consent
    /// (Graph scope type <c>Admin</c>).
    /// </summary>
    IResourceBuilder<ScopeApiExposition> AddScopeWithAdminConsent(
        string name,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string? userConsentDisplayName = null,
        string? userConsentDescription = null);

    /// <summary>
    /// Adds an OAuth2 permission scope that allows admin and user consent
    /// (Graph scope type <c>User</c>).
    /// </summary>
    IResourceBuilder<ScopeApiExposition> AddScopeWithAdminAndUserConsent(
        string name,
        string adminConsentDisplayName,
        string adminConsentDescription,
        string userConsentDisplayName,
        string userConsentDescription);
}
