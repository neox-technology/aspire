using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Starts configuring an AuthOps identity provider (select <c>.Entra(...)</c> next).
/// </summary>
public interface IAuthProviderBuilder
{
    /// <summary>
    /// Configures this provider as Entra ID (v1).
    /// </summary>
    IEntraAuthProviderBuilder Entra(Action<EntraAuthProviderOptions>? configure = null);
}

/// <summary>
/// Entra AuthOps provider builder — register apps with <see cref="AddApp"/>.
/// </summary>
public interface IEntraAuthProviderBuilder
{
    /// <summary>
    /// The Entra provider resource.
    /// </summary>
    IResourceBuilder<EntraAuthProviderResource> Resource { get; }

    /// <summary>
    /// Adds an app registration under this Entra provider.
    /// </summary>
    IResourceBuilder<AuthAppResource> AddApp(string name, Action<AuthAppOptions>? configure = null);
}

/// <summary>
/// Extension methods that register AuthOps provider resources.
/// </summary>
public static class AuthProviderExtensions
{
    /// <summary>
    /// Starts configuring an AuthOps identity provider resource.
    /// </summary>
    public static IAuthProviderBuilder AddAuthProvider(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new AuthProviderBuilder(builder, name);
    }

    private sealed class AuthProviderBuilder(
        IDistributedApplicationBuilder applicationBuilder,
        string name) : IAuthProviderBuilder
    {
        public IEntraAuthProviderBuilder Entra(Action<EntraAuthProviderOptions>? configure = null)
        {
            var options = new EntraAuthProviderOptions();
            configure?.Invoke(options);

            AuthOpsExtensions.EnsureAuthOpsResource(applicationBuilder);
            AuthOpsExtensions.EnsurePrereqAuthStep(applicationBuilder);

            var resource = new EntraAuthProviderResource(name)
            {
                TenantId = options.TenantId
            };

            var tenantParam = AuthOpsExtensions.GetOrAddParameter(
                applicationBuilder,
                $"{name}-tenant-id",
                defaultValue: options.TenantId,
                secret: false);
            resource.TenantIdParameter = tenantParam.Resource;

            var builder = applicationBuilder.AddResource(resource)
                .ExcludeFromManifest()
                .WithInitialState(new CustomResourceSnapshot
                {
                    ResourceType = "AuthProvider",
                    State = KnownResourceStates.Running,
                    Properties = []
                });

            AuthOpsExtensions.EnsurePrereqEntraStep(builder);

            return new EntraAuthProviderBuilder(applicationBuilder, builder);
        }
    }

    private sealed class EntraAuthProviderBuilder(
        IDistributedApplicationBuilder applicationBuilder,
        IResourceBuilder<EntraAuthProviderResource> providerBuilder) : IEntraAuthProviderBuilder
    {
        public IResourceBuilder<EntraAuthProviderResource> Resource => providerBuilder;

        public IResourceBuilder<AuthAppResource> AddApp(string name, Action<AuthAppOptions>? configure = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var options = new AuthAppOptions();
            configure?.Invoke(options);
            options.DisplayName ??= name;

            var provider = providerBuilder.Resource;
            var app = new AuthAppResource(name, provider)
            {
                Options = options,
                TenantIdParameter = provider.TenantIdParameter
                    ?? throw new InvalidOperationException("Entra provider tenant parameter was not created.")
            };

            var clientIdDefault = options.ExistingClientId;
            var clientId = AuthOpsExtensions.GetOrAddParameter(
                applicationBuilder,
                app.GetParameterName("client-id"),
                defaultValue: clientIdDefault,
                secret: false);
            var clientSecret = AuthOpsExtensions.GetOrAddParameter(
                applicationBuilder,
                app.GetParameterName("client-secret"),
                defaultValue: null,
                secret: true);

            app.ClientIdParameter = clientId.Resource;
            app.ClientSecretParameter = clientSecret.Resource;

            provider.RegisterApp(app);

            var appBuilder = applicationBuilder.AddResource(app)
                .ExcludeFromManifest()
                .WithInitialState(new CustomResourceSnapshot
                {
                    ResourceType = "AuthApp",
                    State = KnownResourceStates.Running,
                    Properties = []
                });

            AuthOpsExtensions.RegisterAppPipelineSteps(applicationBuilder, appBuilder);
            return appBuilder;
        }
    }
}
