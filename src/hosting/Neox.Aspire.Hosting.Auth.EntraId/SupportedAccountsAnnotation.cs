using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Desired Entra supported account types for an <see cref="EntraAuthAppRegistrationResource"/>.
/// </summary>
public sealed class SupportedAccountsAnnotation(SupportedAccountsType supportedAccounts) : IResourceAnnotation
{
    /// <summary>
    /// Desired supported accounts (Graph <c>signInAudience</c>).
    /// </summary>
    public SupportedAccountsType SupportedAccounts { get; } = supportedAccounts;
}
