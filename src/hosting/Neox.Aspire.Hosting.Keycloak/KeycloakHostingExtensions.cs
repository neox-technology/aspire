using System.Collections.Concurrent;
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

    public static IResourceBuilder<KeycloakRealmResource> WithOrganizations(
        this IResourceBuilder<KeycloakRealmResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        MutateRealmJson(builder.Resource, EnableOrganizations);
        return builder;
    }

    public static IResourceBuilder<KeycloakRealmResource> WithOrganization(
        this IResourceBuilder<KeycloakRealmResource> builder,
        string name,
        string domain)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        MutateRealmJson(builder.Resource, representation =>
        {
            EnableOrganizations(representation);

            var organization = representation.Organizations!.GetOrAdd(
                o => o.Name == name,
                new OrganizationRepresentation { Name = name, Enabled = true });

            organization.Domains ??= [];
            organization.Domains.GetOrAdd(
                d => d.Name == domain,
                new OrganizationDomainRepresentation { Name = domain });
        });

        return builder;
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

    private static void EnableOrganizations(RealmRepresentation representation)
    {
        representation.OrganizationsEnabled = true;
        representation.Organizations ??= [];
    }

    private static void MutateRealmJson(
        KeycloakRealmResource resource,
        Action<RealmRepresentation> mutate)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(mutate);

        var realmFilePath = Path.Combine(resource.ImportDirectory, $"{resource.Realm}-realm.json");
        var json = File.ReadAllText(realmFilePath);
        var representation = JsonSerializer.Deserialize<RealmRepresentation>(json, KeycloakRealmJsonOptions.Default)
            ?? throw new InvalidOperationException($"Failed to deserialize realm JSON at '{realmFilePath}'.");

        mutate(representation);
        representation.Realm = resource.Realm;

        File.WriteAllText(
            realmFilePath,
            JsonSerializer.Serialize(representation, KeycloakRealmJsonOptions.Default));
    }
}
