using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Neox.Keycloak.Provisioning.Realm;

namespace Neox.Aspire.Hosting.Keycloak;

public static class KeycloakHostingExtensions
{
    private const string RealmImportRelativeRoot = ".aspire/keycloak-realms";

    public static IResourceBuilder<KeycloakRealmResource> AddRealm(
        this IResourceBuilder<KeycloakResource> builder,
        Action<RealmRepresentation>? configure = null,
        string realm = "master")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);

        var keycloak = builder.Resource;
        var importDirectory = Path.GetFullPath(
            Path.Combine(RealmImportRelativeRoot, keycloak.Name),
            builder.ApplicationBuilder.AppHostDirectory);
        Directory.CreateDirectory(importDirectory);

        var representation = new RealmRepresentation
        {
            Realm = realm,
            Enabled = true,
        };
        configure?.Invoke(representation);
        representation.Realm = realm;

        var realmFilePath = Path.Combine(importDirectory, $"{realm}-realm.json");
        File.WriteAllText(
            realmFilePath,
            JsonSerializer.Serialize(representation, KeycloakRealmJsonOptions.Default));

        EnsureRealmImport(builder, importDirectory);

        var resourceName = $"{keycloak.Name}-{realm}";
        var realmResource = new KeycloakRealmResource(resourceName, keycloak, realm, importDirectory);
        return builder.ApplicationBuilder.AddResource(realmResource)
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
        return builder;
    }

    private static void EnsureRealmImport(
        IResourceBuilder<KeycloakResource> builder,
        string importDirectory)
    {
        if (builder.Resource.Annotations.OfType<KeycloakRealmImportAnnotation>().Any())
        {
            return;
        }

        builder.Resource.Annotations.Add(new KeycloakRealmImportAnnotation(importDirectory));
        builder.WithRealmImport(importDirectory);
    }
}
