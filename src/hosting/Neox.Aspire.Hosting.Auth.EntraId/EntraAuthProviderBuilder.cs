using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

internal sealed class EntraAuthProviderBuilder(
    IDistributedApplicationBuilder applicationBuilder,
    IResourceBuilder<EntraAuthOpsResource> providerBuilder) : IEntraAuthProviderBuilder
{
    public IResourceBuilder<EntraAuthOpsResource> Resource => providerBuilder;

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
        };

        var clientIdDefault = options.ExistingClientId;
        var clientId = AuthOpsExtensions.GetOrAddParameter(
            applicationBuilder,
            app.GetParameterName("client-id"),
            defaultValue: clientIdDefault,
            secret: false);
        EntraAppRegistrationParameterPrompt.ConfigureClientIdChoiceInput(clientId, options.DisplayName);

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

        EntraAuthOpsExtensions.RegisterAppPipelineSteps(providerBuilder, appBuilder);
        return appBuilder;
    }
}
