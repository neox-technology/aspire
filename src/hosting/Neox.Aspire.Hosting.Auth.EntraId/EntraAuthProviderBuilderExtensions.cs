#pragma warning disable ASPIREINTERACTION001

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
                secret: false);

        ConfigureTenantChoiceInput(tenantParam, name);
        resource.TenantIdParameter = tenantParam.Resource;

        var providerBuilder = applicationBuilder.AddResource(resource)
            .ExcludeFromManifest()
            .WithParentRelationship(authOps)
            .WithInitialState(AuthDashboardSnapshots.Waiting("AuthProvider"));

        tenantParam.WithParentRelationship(providerBuilder);

        EntraAuthOpsExtensions.EnsurePrereqEntraStep(applicationBuilder, providerBuilder);

        return new EntraAuthProviderBuilder(applicationBuilder, providerBuilder);
    }

    private static void ConfigureTenantChoiceInput(
        IResourceBuilder<ParameterResource> tenantParam,
        string providerResourceName)
    {
        if (tenantParam.Resource.Annotations.OfType<InputGeneratorAnnotation>().Any())
        {
            return;
        }

        tenantParam.WithCustomInput(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = InputType.Choice,
            Label = EntraTenantParameterPrompt.FormatLabel(providerResourceName),
            Description = EntraTenantParameterPrompt.FormatDescription(
                hasTenants: true,
                providerResourceName,
                parameter.Name),
            Required = true,
            AllowCustomChoice = true,
            Options = [],
            DynamicLoading = new InputLoadOptions
            {
                LoadCallback = async context =>
                {
                    var tenants = await EntraTenantEnumerator.TryGetTenantOptionsAsync(
                            cancellationToken: context.CancellationToken)
                        .ConfigureAwait(false);
                    if (tenants.Count > 0)
                    {
                        context.Input.Options = tenants;
                    }
                }
            }
        });
    }
}
