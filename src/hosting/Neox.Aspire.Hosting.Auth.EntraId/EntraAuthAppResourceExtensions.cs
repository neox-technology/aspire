using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra-specific fluent configuration for <see cref="AuthAppResource"/>.
/// </summary>
public static class EntraAuthAppResourceExtensions
{
    /// <summary>
    /// Sets the desired Entra supported account types (Graph <c>signInAudience</c>).
    /// When omitted, AuthOps defaults to <see cref="SupportedAccountsType.SingleTenant"/>.
    /// </summary>
    public static IResourceBuilder<AuthAppResource> WithSupportedAccounts(
        this IResourceBuilder<AuthAppResource> builder,
        SupportedAccountsType supportedAccounts)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithAnnotation(
            new SupportedAccountsAnnotation(supportedAccounts),
            ResourceAnnotationMutationBehavior.Replace);
    }
}
