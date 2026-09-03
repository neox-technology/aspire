using System.Collections.Concurrent;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Hosting;
using Neox.Keycloak.Provisioning.Realm;

namespace Neox.Aspire.Hosting.Keycloak;

public static class KeycloakHostingExtensions
{
    private const string RealmImportRelativeRoot = ".aspire/keycloak-realms";
    private static readonly string[] LocalLoopbackHosts = ["127.0.0.1", "localhost", "[::1]"];

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

    public static IResourceBuilder<KeycloakOidcClientResource> AddOidcClient(
        this IResourceBuilder<KeycloakRealmResource> builder,
        string name,
        string clientId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var oidcClient = new KeycloakOidcClientResource(name, builder.Resource, clientId);

        MutateRealmJson(builder.Resource, representation =>
        {
            representation.Clients ??= [];
            var client = representation.Clients.GetOrAdd(
                c => c.ClientId == clientId,
                new ClientRepresentation
                {
                    ClientId = clientId,
                    Enabled = true,
                    PublicClient = true,
                    BearerOnly = false,
                    StandardFlowEnabled = false,
                    DirectAccessGrantsEnabled = false,
                    ImplicitFlowEnabled = false,
                    ServiceAccountsEnabled = false,
                });
            ApplyOidcClientDefaults(client);
        });

        return builder.ApplicationBuilder.AddResource(oidcClient)
            .ExcludeFromManifest();
    }

    public static IResourceBuilder<KeycloakIdentityProviderResource> AddIdentityProvider(
        this IResourceBuilder<KeycloakRealmResource> builder,
        string alias,
        string providerId,
        Action<IdentityProviderRepresentation>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        var resourceName = $"{builder.Resource.Name}-idp-{alias}";
        var existing = builder.ApplicationBuilder.Resources
            .OfType<KeycloakIdentityProviderResource>()
            .FirstOrDefault(r =>
                string.Equals(r.Name, resourceName, StringComparison.OrdinalIgnoreCase));

        MutateRealmJson(builder.Resource, representation =>
        {
            representation.IdentityProviders ??= [];
            var idp = representation.IdentityProviders.GetOrAdd(
                i => string.Equals(i.Alias, alias, StringComparison.Ordinal),
                new IdentityProviderRepresentation
                {
                    Alias = alias,
                    ProviderId = providerId,
                    Enabled = true,
                });

            idp.Alias = alias;
            idp.ProviderId = providerId;
            if (idp.Enabled is null)
            {
                idp.Enabled = true;
            }

            configure?.Invoke(idp);
            idp.Alias = alias;
            idp.ProviderId = providerId;
        });

        if (existing is not null)
        {
            return builder.ApplicationBuilder.CreateResourceBuilder(existing);
        }

        var identityProvider = new KeycloakIdentityProviderResource(
            resourceName,
            builder.Resource,
            alias,
            providerId);

        return builder.ApplicationBuilder.AddResource(identityProvider)
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

        SyncClientRedirectRealmJson(builder.Resource, client => ApplyJwtClientAuthDefaults(client, builder.Resource));
        return builder;
    }

    /// <summary>
    /// Registers loopback redirect URIs for local development (any ephemeral port on HTTP).
    /// No-op outside the Development environment.
    /// </summary>
    /// <param name="builder">The JWT client resource builder.</param>
    /// <param name="path">
    /// Redirect path (for example <c>/swagger/oauth2-redirect.html</c>).
    /// </param>
    public static IResourceBuilder<KeycloakJwtClientResource> WithLocalRedirectUri(
        this IResourceBuilder<KeycloakJwtClientResource> builder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddLocalRedirectUris(builder.Resource, builder.ApplicationBuilder, path);
        SyncClientRedirectRealmJson(builder.Resource, client => ApplyJwtClientAuthDefaults(client, builder.Resource));
        return builder;
    }

    public static IResourceBuilder<KeycloakOidcClientResource> WithRedirectUrl(
        this IResourceBuilder<KeycloakOidcClientResource> builder,
        Uri url)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(url);

        AddRedirectUrl(builder.Resource, url);
        SyncClientRedirectRealmJson(builder.Resource, ApplyOidcClientDefaults);
        return builder;
    }

    /// <summary>
    /// Registers loopback redirect URIs for local development (any ephemeral port on HTTP).
    /// No-op outside the Development environment.
    /// </summary>
    public static IResourceBuilder<KeycloakOidcClientResource> WithLocalRedirectUri(
        this IResourceBuilder<KeycloakOidcClientResource> builder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(builder);
        AddLocalRedirectUris(builder.Resource, builder.ApplicationBuilder, path);
        SyncClientRedirectRealmJson(builder.Resource, ApplyOidcClientDefaults);
        return builder;
    }

    /// <summary>
    /// Adds an <c>access_as_user</c> client scope with an audience mapper and assigns it as a default scope on the SPA client.
    /// </summary>
    public static IResourceBuilder<KeycloakOidcClientResource> WithApiAudience(
        this IResourceBuilder<KeycloakOidcClientResource> builder,
        string audience)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        var clientId = builder.Resource.ClientId;
        MutateRealmJson(builder.Resource.Parent, representation =>
        {
            EnsureAccessAsUserScope(representation, audience);

            var client = representation.Clients?.FirstOrDefault(c => c.ClientId == clientId);
            if (client is null)
            {
                return;
            }

            client.DefaultClientScopes ??= [];
            foreach (var scope in new[] { "access_as_user", "profile", "openid" })
            {
                client.DefaultClientScopes.GetOrAdd(existing => existing == scope, scope);
            }
        });

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
        var authServerUrl = ResolveKeycloakAuthServerUrl(keycloakBuilder);

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
        IResourceBuilder<KeycloakOidcClientResource> oidcClient,
        string sectionName = "KEYCLOAK_")
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(oidcClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        var client = oidcClient.Resource;
        var realm = client.Parent;
        var keycloak = client.Keycloak;
        var keycloakBuilder = builder.ApplicationBuilder.CreateResourceBuilder(keycloak);
        var realmBuilder = builder.ApplicationBuilder.CreateResourceBuilder(realm);
        var authServerUrl = ResolveKeycloakAuthServerUrl(keycloakBuilder);

        builder.WithEnvironment($"{sectionName}URL", authServerUrl);
        builder.WithEnvironment($"{sectionName}REALM", realm.Realm);
        builder.WithEnvironment($"{sectionName}CLIENT_ID", client.ClientId);

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

    private static EndpointReference ResolveKeycloakAuthServerUrl(IResourceBuilder<KeycloakResource> keycloakBuilder)
    {
        ArgumentNullException.ThrowIfNull(keycloakBuilder);

        var hasHttpsEndpoint = keycloakBuilder.Resource.Annotations
            .OfType<EndpointAnnotation>()
            .Any(endpoint => string.Equals(endpoint.Name, "https", StringComparison.OrdinalIgnoreCase));

        return hasHttpsEndpoint
            ? keycloakBuilder.GetEndpoint("https")
            : keycloakBuilder.GetEndpoint("http");
    }

    private static void SyncClientRedirectRealmJson(
        IKeycloakRedirectClient client,
        Action<ClientRepresentation> applyDefaults)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(applyDefaults);

        MutateRealmJson(client.Parent, representation =>
        {
            var clientRep = representation.Clients?.FirstOrDefault(c => c.ClientId == client.ClientId)
                ?? throw new InvalidOperationException(
                    $"Client '{client.ClientId}' was not found in realm '{client.Parent.Realm}'.");

            applyDefaults(clientRep);

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

            if (client.UseDevWebOriginWildcard)
            {
                // Keycloak only accepts "*" as a CORS wildcard (not "+") for ephemeral loopback ports.
                clientRep.WebOrigins.GetOrAdd(o => o == "*", "*");
            }

            clientRep.StandardFlowEnabled = true;
        });
    }

    private static void AddRedirectUrl(IKeycloakRedirectClient client, Uri url)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(url);

        if (!client.RedirectUrls.Any(existing =>
                string.Equals(existing.AbsoluteUri, url.AbsoluteUri, StringComparison.Ordinal)))
        {
            client.RedirectUrls.Add(url);
        }
    }

    private static void AddLocalRedirectUris(
        IKeycloakRedirectClient client,
        IDistributedApplicationBuilder applicationBuilder,
        string path)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(applicationBuilder);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!applicationBuilder.Environment.IsDevelopment())
        {
            return;
        }

        client.UseDevWebOriginWildcard = true;

        foreach (var redirectUri in CreateLocalLoopbackRedirectUris(path))
        {
            AddRedirectUrl(client, redirectUri);
        }
    }

    private static void ApplyOidcClientDefaults(ClientRepresentation client)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.Protocol = "openid-connect";
        client.PublicClient = true;
    }

    private static void EnsureAccessAsUserScope(RealmRepresentation representation, string audience)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);

        representation.ClientScopes ??= [];
        var scope = representation.ClientScopes.GetOrAdd(
            existing => existing.Name == "access_as_user",
            new ClientScopeRepresentation
            {
                Name = "access_as_user",
                Description = "Allow API access as the signed-in user.",
                Protocol = "openid-connect",
            });

        scope.Attributes = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
        {
            ["include.in.token.scope"] = "true",
            ["display.on.consent.screen"] = "true",
        };

        scope.ProtocolMappers ??= [];
        scope.ProtocolMappers.GetOrAdd(
            mapper => mapper.Name == "audience",
            new ProtocolMapperRepresentation
            {
                Name = "audience",
                Protocol = "openid-connect",
                ProtocolMapper = "oidc-audience-mapper",
                Config = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["included.client.audience"] = audience,
                    ["access.token.claim"] = "true",
                    ["id.token.claim"] = "false",
                },
            });
        scope.ProtocolMappers.GetOrAdd(
            mapper => mapper.Name == "preferred_username",
            new ProtocolMapperRepresentation
            {
                Name = "preferred_username",
                Protocol = "openid-connect",
                ProtocolMapper = "oidc-usermodel-property-mapper",
                Config = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["user.attribute"] = "username",
                    ["claim.name"] = "preferred_username",
                    ["jsonType.label"] = "String",
                    ["access.token.claim"] = "true",
                    ["id.token.claim"] = "true",
                    ["userinfo.token.claim"] = "true",
                },
            });
    }

    private static IEnumerable<Uri> CreateLocalLoopbackRedirectUris(string path)
    {
        var normalizedPath = path.Trim();
        if (!normalizedPath.StartsWith('/'))
        {
            normalizedPath = $"/{normalizedPath}";
        }

        foreach (var host in LocalLoopbackHosts)
        {
            yield return new Uri($"http://{host}{normalizedPath}", UriKind.Absolute);
        }
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

    internal static void MutateRealmJson(
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
