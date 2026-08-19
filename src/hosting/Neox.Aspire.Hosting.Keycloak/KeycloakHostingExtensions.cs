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

    public static IResourceBuilder<KeycloakJwtClientResource> AddJwtClient(
        this IResourceBuilder<KeycloakRealmResource> builder,
        string name,
        string clientId,
        IResourceBuilder<ParameterResource> clientSecret,
        string? audience = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(clientSecret);

        audience ??= clientId;
        var secretValue = ResolveParameterDefault(clientSecret.Resource);

        var jwtClient = new KeycloakJwtClientResource(
            name,
            builder.Resource,
            clientId,
            clientSecret.Resource,
            audience);

        MutateRealmJson(builder.Resource, representation =>
        {
            representation.Clients ??= [];
            var client = representation.Clients.GetOrAdd(
                c => c.ClientId == clientId,
                new ClientRepresentation
                {
                    ClientId = clientId,
                    Enabled = true,
                    PublicClient = false,
                    BearerOnly = false,
                    ClientAuthenticatorType = "client-secret",
                    Secret = secretValue,
                    StandardFlowEnabled = false,
                    DirectAccessGrantsEnabled = false,
                    ServiceAccountsEnabled = false,
                });
            ApplyJwtClientAuthDefaults(client, jwtClient);
        });

        return builder.ApplicationBuilder.AddResource(jwtClient)
            .ExcludeFromManifest();
    }

    public static IResourceBuilder<KeycloakJwtClientResource> WithRedirectUrl(
        this IResourceBuilder<KeycloakJwtClientResource> builder,
        Uri url)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(url);

        if (!builder.Resource.RedirectUrls.Any(existing =>
                string.Equals(existing.AbsoluteUri, url.AbsoluteUri, StringComparison.Ordinal)))
        {
            builder.Resource.RedirectUrls.Add(url);
        }

        SyncJwtClientRealmJson(builder.Resource);
        return builder;
    }

    public static IResourceBuilder<TResource> WithKeycloakJwtBearer<TResource>(
        this IResourceBuilder<TResource> builder,
        IResourceBuilder<KeycloakJwtClientResource> jwtClient,
        string sectionName = "Keycloak__")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(jwtClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var client = jwtClient.Resource;
        var realm = client.Parent;
        var keycloak = client.Keycloak;
        var keycloakBuilder = builder.ApplicationBuilder.CreateResourceBuilder(keycloak);
        var realmBuilder = builder.ApplicationBuilder.CreateResourceBuilder(realm);
        var authServerUrl = keycloakBuilder.GetEndpoint("http");

        builder.WithEnvironment($"{sectionName}AuthServerUrl", authServerUrl);
        builder.WithEnvironment($"{sectionName}Realm", realm.Realm);
        builder.WithEnvironment(
            $"{sectionName}Authority",
            ReferenceExpression.Create($"{authServerUrl}/realms/{realm.Realm}"));
        builder.WithEnvironment($"{sectionName}ClientId", client.ClientId);
        builder.WithEnvironment($"{sectionName}Audience", client.Audience);
        builder.WithEnvironment($"{sectionName}ClientSecret", client.ClientSecret);
        builder.WithEnvironment($"{sectionName}ServiceName", keycloak.Name);

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, keycloak)))
        {
            builder.WaitFor(keycloakBuilder);
        }

        if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, realm)))
        {
            builder.WaitFor(realmBuilder);
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

    private static void SyncJwtClientRealmJson(KeycloakJwtClientResource client)
    {
        ArgumentNullException.ThrowIfNull(client);

        MutateRealmJson(client.Parent, representation =>
        {
            var clientRep = representation.Clients?.FirstOrDefault(c => c.ClientId == client.ClientId)
                ?? throw new InvalidOperationException(
                    $"Client '{client.ClientId}' was not found in realm '{client.Parent.Realm}'.");

            ApplyJwtClientAuthDefaults(clientRep, client);

            if (client.RedirectUrls.Count == 0)
            {
                return;
            }

            clientRep.RedirectUris ??= [];
            clientRep.WebOrigins ??= [];
            foreach (var redirectUrl in client.RedirectUrls)
            {
                var redirectUri = redirectUrl.AbsoluteUri;
                clientRep.RedirectUris.GetOrAdd(u => u == redirectUri, redirectUri);

                var origin = GetWebOrigin(redirectUrl);
                clientRep.WebOrigins.GetOrAdd(o => o == origin, origin);
            }

            clientRep.StandardFlowEnabled = true;
        });
    }

    private static void ApplyJwtClientAuthDefaults(
        ClientRepresentation client,
        KeycloakJwtClientResource resource)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(resource);

        client.Protocol = "openid-connect";
        client.ProtocolMappers ??= [];
        client.ProtocolMappers.GetOrAdd(
            mapper => mapper.Name == "audience",
            new ProtocolMapperRepresentation
            {
                Name = "audience",
                Protocol = "openid-connect",
                ProtocolMapper = "oidc-audience-mapper",
                Config = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["included.client.audience"] = resource.Audience,
                    ["access.token.claim"] = "true",
                    ["id.token.claim"] = "false",
                },
            });
    }

    private static string GetWebOrigin(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        return url.IsDefaultPort
            ? $"{url.Scheme}://{url.Host}"
            : $"{url.Scheme}://{url.Host}:{url.Port}";
    }

    private static string ResolveParameterDefault(ParameterResource parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        if (parameter.Default is not null)
        {
            try
            {
                var fromDefault = parameter.Default.GetDefaultValue();
                if (!string.IsNullOrWhiteSpace(fromDefault))
                {
                    return fromDefault;
                }
            }
            catch
            {
                // Ignore non-constant defaults at registration time.
            }
        }

#pragma warning disable CS0618
        try
        {
            var fromValue = parameter.Value;
            if (!string.IsNullOrWhiteSpace(fromValue))
            {
                return fromValue;
            }
        }
        catch
        {
            // Value throws when the parameter has no default and is unresolved.
        }
#pragma warning restore CS0618

        throw new InvalidOperationException(
            $"Parameter '{parameter.Name}' has no resolvable default value for realm import.");
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
