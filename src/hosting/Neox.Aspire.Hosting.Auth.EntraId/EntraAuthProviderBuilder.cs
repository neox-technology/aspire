using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

internal sealed class EntraAuthProviderBuilder(
    IDistributedApplicationBuilder applicationBuilder,
    IResourceBuilder<EntraAuthOpsResource> providerBuilder) : IEntraAuthProviderBuilder
{
    public IResourceBuilder<EntraAuthOpsResource> Resource => providerBuilder;

    public IResourceBuilder<EntraAuthAppRegistrationResource> AddAppRegistration(string name, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var provider = providerBuilder.Resource;
        var app = new EntraAuthAppRegistrationResource(name, provider, displayName)
        {
            TenantIdParameter = provider.TenantIdParameter
        };

        var clientId = AuthOpsExtensions.GetOrAddParameter(
            applicationBuilder,
            app.GetParameterName("client-id"),
            defaultValue: null,
            secret: false);
        EntraAppRegistrationParameterPrompt.ConfigureClientIdChoiceInput(
            clientId,
            name,
            displayName,
            provider.Name,
            provider.TenantIdParameter);

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
            .WithInitialState(AuthDashboardSnapshots.Waiting("EntraAuthAppRegistration"))
            .WithProvisionAuthCommand();

        clientId.WithParentRelationship(appBuilder);
        clientSecret.WithParentRelationship(appBuilder);

        EntraAuthOpsExtensions.RegisterAppPipelineSteps(providerBuilder, appBuilder);
        return appBuilder;
    }
}
