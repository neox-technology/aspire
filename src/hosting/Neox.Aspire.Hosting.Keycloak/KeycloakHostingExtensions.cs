using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

public static class KeycloakHostingExtensions
{
    public static IResourceBuilder<KeycloakRealmResource> AddKeycloakRealm(
        this IDistributedApplicationBuilder builder,
        string name,
        string realm,
        string authServerUrl,
        string? clientId = null,
        string? clientSecret = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(authServerUrl);

        var resource = new KeycloakRealmResource(name, realm, authServerUrl, clientId, clientSecret);
        return builder.AddResource(resource)
            .ExcludeFromManifest();
    }

    public static IResourceBuilder<TResource> WithKeycloakWebApi<TResource>(
        this IResourceBuilder<TResource> builder,
        IResourceBuilder<KeycloakRealmResource> realm,
        string sectionName = "Keycloak__")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var model = realm.Resource;
        builder.WithEnvironment($"{sectionName}AuthServerUrl", model.AuthServerUrl);
        builder.WithEnvironment($"{sectionName}Realm", model.Realm);
        builder.WithEnvironment($"{sectionName}Authority", $"{model.AuthServerUrl}/realms/{model.Realm}");

        if (!string.IsNullOrWhiteSpace(model.ClientId))
        {
            builder.WithEnvironment($"{sectionName}ClientId", model.ClientId);
            builder.WithEnvironment($"{sectionName}Audience", model.ClientId);
        }

        if (model.ClientSecret is not null)
        {
            builder.WithEnvironment($"{sectionName}ClientSecret", model.ClientSecret);
        }

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, model)))
        {
            builder.WaitFor(realm);
        }

        return builder;
    }

    public static IResourceBuilder<TResource> WithKeycloakSpa<TResource>(
        this IResourceBuilder<TResource> builder,
        IResourceBuilder<KeycloakRealmResource> realm,
        string sectionName = "KEYCLOAK_")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var model = realm.Resource;
        builder.WithEnvironment($"{sectionName}URL", model.AuthServerUrl);
        builder.WithEnvironment($"{sectionName}REALM", model.Realm);
        if (!string.IsNullOrWhiteSpace(model.ClientId))
        {
            builder.WithEnvironment($"{sectionName}CLIENT_ID", model.ClientId);
        }

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, model)))
        {
            builder.WaitFor(realm);
        }

        return builder;
    }
}
