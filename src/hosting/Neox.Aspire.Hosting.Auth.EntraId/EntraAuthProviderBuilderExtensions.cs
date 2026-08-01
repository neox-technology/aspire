using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra ID extensions for <see cref="IAuthProviderBuilder"/>.
/// </summary>
public static class EntraAuthProviderBuilderExtensions
{
    /// <summary>
    /// Configures this provider as Entra ID.
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

        var resource = new EntraAuthProviderResource(name)
        {
            TenantId = options.TenantId,
            AuthorityFormatter = static tenantId => $"https://login.microsoftonline.com/{tenantId}"
        };

        var tenantParam = AuthOpsExtensions.GetOrAddParameter(
            applicationBuilder,
            $"{name}-tenant-id",
            defaultValue: options.TenantId,
            secret: false);
        resource.TenantIdParameter = tenantParam.Resource;

        var providerBuilder = applicationBuilder.AddResource(resource)
            .ExcludeFromManifest()
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "AuthProvider",
                State = KnownResourceStates.Running,
                Properties = []
            });

        EntraAuthOpsExtensions.EnsurePrereqEntraStep(providerBuilder);

        return new EntraAuthProviderBuilder(applicationBuilder, providerBuilder);
    }
}
