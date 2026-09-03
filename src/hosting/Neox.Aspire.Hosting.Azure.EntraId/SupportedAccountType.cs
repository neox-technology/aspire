namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Graph <c>signInAudience</c>: which Microsoft accounts can sign in to the application.
/// </summary>
public enum SupportedAccountType
{
    /// <summary>
    /// Accounts in this organizational directory only (Graph <c>AzureADMyOrg</c>). Default.
    /// </summary>
    AzureADMyOrg,

    /// <summary>
    /// Accounts in any organizational directory (Graph <c>AzureADMultipleOrgs</c>).
    /// </summary>
    AzureADMultipleOrgs,

    /// <summary>
    /// Accounts in any organizational directory and personal Microsoft accounts
    /// (Graph <c>AzureADandPersonalMicrosoftAccount</c>).
    /// </summary>
    AzureADandPersonalMicrosoftAccount,

    /// <summary>
    /// Personal Microsoft accounts only (Graph <c>PersonalMicrosoftAccount</c>).
    /// </summary>
    PersonalMicrosoftAccount
}
