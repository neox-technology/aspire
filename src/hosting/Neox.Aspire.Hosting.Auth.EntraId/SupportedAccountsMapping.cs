using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Maps <see cref="SupportedAccountsType"/> to/from Graph <c>signInAudience</c> and resolves desired state.
/// </summary>
public static class SupportedAccountsMapping
{
    /// <summary>
    /// Graph value for <see cref="SupportedAccountsType.SingleTenant"/> (AuthOps default).
    /// </summary>
    public const string DefaultSignInAudience = "AzureADMyOrg";

    /// <summary>
    /// Converts an AuthOps enum value to the Graph <c>signInAudience</c> string.
    /// </summary>
    public static string ToSignInAudience(SupportedAccountsType supportedAccounts) =>
        supportedAccounts switch
        {
            SupportedAccountsType.SingleTenant => "AzureADMyOrg",
            SupportedAccountsType.MultiTenant => "AzureADMultipleOrgs",
            SupportedAccountsType.MultiTenantAndPersonal => "AzureADandPersonalMicrosoftAccount",
            SupportedAccountsType.PersonalMicrosoftAccount => "PersonalMicrosoftAccount",
            _ => throw new ArgumentOutOfRangeException(nameof(supportedAccounts), supportedAccounts, null)
        };

    /// <summary>
    /// Converts a Graph <c>signInAudience</c> string to <see cref="SupportedAccountsType"/>.
    /// Unknown or null values map to <see cref="SupportedAccountsType.SingleTenant"/>.
    /// </summary>
    public static SupportedAccountsType FromSignInAudience(string? signInAudience) =>
        signInAudience switch
        {
            "AzureADMultipleOrgs" => SupportedAccountsType.MultiTenant,
            "AzureADandPersonalMicrosoftAccount" => SupportedAccountsType.MultiTenantAndPersonal,
            "PersonalMicrosoftAccount" => SupportedAccountsType.PersonalMicrosoftAccount,
            _ => SupportedAccountsType.SingleTenant
        };

    /// <summary>
    /// Desired supported accounts from <see cref="SupportedAccountsAnnotation"/>, or
    /// <see cref="SupportedAccountsType.SingleTenant"/> when absent.
    /// </summary>
    public static SupportedAccountsType GetDesired(AuthAppResource app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.Annotations.OfType<SupportedAccountsAnnotation>().FirstOrDefault()?.SupportedAccounts
            ?? SupportedAccountsType.SingleTenant;
    }

    /// <summary>
    /// Desired Graph <c>signInAudience</c> for the Auth app.
    /// </summary>
    public static string GetDesiredSignInAudience(AuthAppResource app) =>
        ToSignInAudience(GetDesired(app));
}
