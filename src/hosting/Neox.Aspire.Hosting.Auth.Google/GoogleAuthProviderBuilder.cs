using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

internal sealed class GoogleAuthProviderBuilder(
    IDistributedApplicationBuilder applicationBuilder,
    IResourceBuilder<GoogleAuthOpsResource> providerBuilder) : IGoogleAuthProviderBuilder
{
    public IResourceBuilder<GoogleAuthOpsResource> Resource => providerBuilder;

    public IResourceBuilder<GoogleAuthAppRegistrationResource> AddAppRegistration(string name, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var provider = providerBuilder.Resource;
        var app = new GoogleAuthAppRegistrationResource(name, provider, displayName)
        {
            TenantIdParameter = provider.ProjectIdParameter
        };

        var clientId = AuthOpsExtensions.GetOrAddParameter(
            applicationBuilder,
            app.GetParameterName("client-id"),
            defaultValue: null,
            secret: false);
        GoogleOauthClientParameterPrompt.ConfigureClientIdChoiceInput(
            clientId,
            name,
            displayName,
            provider.Name,
            provider.ProjectIdParameter);

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
            .WithParentRelationship(providerBuilder)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "GoogleAuthAppRegistration",
                State = KnownResourceStates.Running,
                Properties = []
            });

        clientId.WithParentRelationship(appBuilder);
        clientSecret.WithParentRelationship(appBuilder);

        GoogleAuthOpsExtensions.RegisterAppPipelineSteps(providerBuilder, appBuilder);
        return appBuilder;
    }
}
