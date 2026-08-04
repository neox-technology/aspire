using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra ID extensions for <see cref="IAuthProviderBuilder"/>.
/// </summary>
public static class EntraAuthProviderBuilderExtensions
{
    /// <summary>
    /// Configures this provider as Entra ID, creating an <see cref="EntraAuthOpsResource"/>.
    /// </summary>
    public static IEntraAuthProviderBuilder Entra(
        this IAuthProviderBuilder builder,
        Action<EntraAuthProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new EntraAuthProviderOptions();
        configure?.Invoke(options);

        var applicationBuilder = builder.ApplicationBuilder;
        var name = builder.Name;

        applicationBuilder.AddEntraAuthDashboardServices();

        var authOps = AuthOpsExtensions.EnsureAuthOpsResource(applicationBuilder);
        // Entra owns dashboard status for auth-ops; start Waiting until probes run.
        authOps.WithInitialState(AuthDashboardSnapshots.Waiting("AuthOps"));

        var resource = new EntraAuthOpsResource(name, authOps.Resource)
        {
            AuthorityExpression = static tenant =>
                ReferenceExpression.Create($"https://login.microsoftonline.com/{tenant}")
        };

        var tenantParam = options.TenantId
            ?? AuthOpsExtensions.GetOrAddParameter(
                applicationBuilder,
                $"{name}-tenant-id",
                defaultValue: null,
                secret: false,
                publishEmptyDefault: true);

        resource.TenantIdParameter = tenantParam.Resource;

        var providerBuilder = applicationBuilder.AddResource(resource)
            .ExcludeFromManifest()
            .WithParentRelationship(authOps)
            .WithInitialState(AuthDashboardSnapshots.Waiting("AuthProvider"))
            .WithSelectTenantCommand();

        tenantParam.WithParentRelationship(providerBuilder);

        EntraAuthOpsExtensions.EnsurePrereqEntraStep(applicationBuilder, providerBuilder);

        return new EntraAuthProviderBuilder(applicationBuilder, providerBuilder);
    }
}
