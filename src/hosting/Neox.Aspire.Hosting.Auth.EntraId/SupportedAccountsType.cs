namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra ID supported account types (maps to Graph <c>signInAudience</c>).
/// </summary>
public enum SupportedAccountsType
{
    /// <summary>
    /// Accounts in this organizational directory only (<c>AzureADMyOrg</c>). Default.
    /// </summary>
    SingleTenant = 0,

    /// <summary>
    /// Accounts in any organizational directory (<c>AzureADMultipleOrgs</c>).
    /// </summary>
    MultiTenant,

    /// <summary>
    /// Accounts in any organizational directory and personal Microsoft accounts
    /// (<c>AzureADandPersonalMicrosoftAccount</c>).
    /// </summary>
    MultiTenantAndPersonal,

    /// <summary>
    /// Personal Microsoft accounts only (<c>PersonalMicrosoftAccount</c>).
    /// </summary>
    PersonalMicrosoftAccount
}
