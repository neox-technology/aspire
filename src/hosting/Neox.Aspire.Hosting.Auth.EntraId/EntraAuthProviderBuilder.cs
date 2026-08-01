using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

internal sealed class EntraAuthProviderBuilder(
    IDistributedApplicationBuilder applicationBuilder,
    IResourceBuilder<EntraAuthOpsResource> providerBuilder) : IEntraAuthProviderBuilder
{
    public IResourceBuilder<EntraAuthOpsResource> Resource => providerBuilder;

    public IResourceBuilder<AuthAppResource> AddAppRegistration(string name, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var provider = providerBuilder.Resource;
        var app = new AuthAppResource(name, provider, displayName)
        {
            TenantIdParameter = provider.TenantIdParameter
        };

        var clientId = AuthOpsExtensions.GetOrAddParameter(
            applicationBuilder,
            app.GetParameterName("client-id"),
            defaultValue: null,
            secret: false);
        EntraAppRegistrationParameterPrompt.ConfigureClientIdChoiceInput(clientId, displayName);

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
