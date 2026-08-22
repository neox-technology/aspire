using System.Collections.Concurrent;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Keycloak;
using Neox.Keycloak.Provisioning.Realm;

namespace Neox.Aspire.Hosting.Keycloak.EntraId;

/// <summary>
/// Extensions that wire an Entra ID app registration as a Keycloak OIDC identity provider.
/// </summary>
public static class KeycloakEntraIdHostingExtensions
{
    /// <summary>
    /// Registers an Entra ID app registration as a Keycloak OIDC identity provider on the realm.
    /// The registration must already have <c>WithSecret</c>. Client id and secret are written into
    /// realm JSON at initialize time by a non-parented config gate after the registration and
    /// password credentials are ready. Keycloak <c>WaitFor</c>s that gate (not the IdP child).
    /// </summary>
    public static IResourceBuilder<KeycloakIdentityProviderResource> AddEntraIdIdentityProvider(
        this IResourceBuilder<KeycloakRealmResource> realm,
        EntraIdInstance instance,
        IResourceBuilder<AzureEntraIdAppRegistrationResource> appRegistration,
        string alias = "microsoft",
        string? displayName = null,
        Uri? brokerRedirectUri = null)
    {
        ArgumentNullException.ThrowIfNull(realm);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(appRegistration);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var registration = appRegistration.Resource;
        if (registration.PasswordCredentials.Count == 0)
        {
            throw new InvalidOperationException(
                $"App registration '{registration.Name}' must call WithSecret before AddEntraIdIdentityProvider.");
        }

        if (brokerRedirectUri is not null)
        {
            appRegistration
                .AddWebApplication($"{alias}-broker")
                .WithRedirectUri(brokerRedirectUri);
        }

        var idp = realm.AddIdentityProvider(alias, "oidc", representation =>
        {
            representation.DisplayName = displayName ?? "Microsoft";
            representation.Enabled = true;
        });

        KeycloakHostingExtensions.MutateRealmJson(
            realm.Resource,
            representation => EnsureEmailClaimMappers(representation, alias));

        var config = new KeycloakEntraIdConfigResource($"{idp.Resource.Name}-config", idp.Resource);
        var configBuilder = realm.ApplicationBuilder.AddResource(config)
            .ExcludeFromManifest()
            .WithAnnotation(new KeycloakEntraIdIdentityProviderAnnotation
            {
                Instance = instance,
                AppRegistration = registration,
            })
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "KeycloakEntraIdConfig",
                State = KnownResourceStates.NotStarted,
                Properties = [],
            })
            .WaitFor(appRegistration);

        foreach (var credential in registration.PasswordCredentials)
        {
            configBuilder = configBuilder.WaitFor(
                configBuilder.ApplicationBuilder.CreateResourceBuilder(credential));
        }

        configBuilder = configBuilder.OnInitializeResource(async (resource, evt, ct) =>
        {
            try
            {
                await EnsureEntraIdConfigAsync(resource, evt.Services, evt.Logger, ct).ConfigureAwait(false);
                await evt.Notifications.PublishUpdateAsync(resource, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot(KnownResourceStates.Running, KnownResourceStateStyles.Success),
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                evt.Logger.LogError(
                    ex,
                    "Failed to configure Keycloak Entra ID identity provider '{Alias}'.",
                    resource.IdentityProvider.Alias);
                await evt.Notifications.PublishUpdateAsync(resource, snapshot => snapshot with
                {
                    State = new ResourceStateSnapshot(KnownResourceStates.FailedToStart, KnownResourceStateStyles.Error),
                }).ConfigureAwait(false);
            }
        });

        var keycloakBuilder = realm.ApplicationBuilder.CreateResourceBuilder(realm.Resource.Parent);
        keycloakBuilder.WaitFor(configBuilder);

        return idp;
    }

    private static async Task EnsureEntraIdConfigAsync(
        KeycloakEntraIdConfigResource config,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var executionContext = services.GetService<DistributedApplicationExecutionContext>();
        if (executionContext is null || !executionContext.IsRunMode)
        {
            return;
        }

        var identityProvider = config.IdentityProvider;
        var annotation = config.Annotations.OfType<KeycloakEntraIdIdentityProviderAnnotation>().SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Config resource '{config.Name}' is missing KeycloakEntraIdIdentityProviderAnnotation.");

        var appRegistration = annotation.AppRegistration;
        var clientId = await appRegistration.ClientId.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(clientId))
        {
            throw new InvalidOperationException(
                $"App registration '{appRegistration.Name}' ClientId output is empty.");
        }

        var secretParameter = appRegistration.PasswordCredentials[0].SecretParameter;
        var clientSecret = await secretParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException(
                $"Client secret parameter '{secretParameter.Name}' resolved to an empty value.");
        }

        var configuration = services.GetService<IConfiguration>();
        var tenantId = configuration?["Azure:TenantId"];
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException(
                "AppHost configuration 'Azure:TenantId' is required to build the Entra OIDC discovery endpoint.");
        }

        var instanceUrl = await annotation.Instance.ResolveBaseUrlAsync(cancellationToken).ConfigureAwait(false);
        var discoveryEndpoint =
            $"{instanceUrl.TrimEnd('/')}/{tenantId.Trim()}/v2.0/.well-known/openid-configuration";

        // Keycloak does not resolve discovery at login time when only discoveryEndpoint is set
        // (Admin UI calls importFromUrl and persists authorizationUrl/tokenUrl/…). Fetch metadata here.
        var metadata = await FetchOidcDiscoveryAsync(discoveryEndpoint, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Writing Entra OIDC identity provider '{Alias}' into Keycloak realm '{Realm}' (discovery {Discovery}).",
            identityProvider.Alias,
            identityProvider.Parent.Realm,
            discoveryEndpoint);

        KeycloakHostingExtensions.MutateRealmJson(identityProvider.Parent, representation =>
        {
            representation.IdentityProviders ??= [];
            var idp = representation.IdentityProviders.GetOrAdd(
                i => string.Equals(i.Alias, identityProvider.Alias, StringComparison.Ordinal),
                new IdentityProviderRepresentation
                {
                    Alias = identityProvider.Alias,
                    ProviderId = identityProvider.ProviderId,
                    Enabled = true,
                });

            idp.Alias = identityProvider.Alias;
            idp.ProviderId = identityProvider.ProviderId;
            idp.Enabled = true;
            idp.Config ??= new ConcurrentDictionary<string, string>();
            idp.Config["clientId"] = clientId;
            idp.Config["clientSecret"] = clientSecret;
            idp.Config["clientAuthMethod"] = "client_secret_post";
            idp.Config["defaultScope"] = "openid profile email";
            idp.Config["useDiscoveryEndpoint"] = "true";
            idp.Config["discoveryEndpoint"] = discoveryEndpoint;
            idp.Config["authorizationUrl"] = metadata.AuthorizationUrl;
            idp.Config["tokenUrl"] = metadata.TokenUrl;
            idp.Config["jwksUrl"] = metadata.JwksUrl;
            idp.Config["issuer"] = metadata.Issuer;
            if (!string.IsNullOrEmpty(metadata.UserInfoUrl))
            {
                idp.Config["userInfoUrl"] = metadata.UserInfoUrl;
            }

            if (!string.IsNullOrEmpty(metadata.LogoutUrl))
            {
                idp.Config["logoutUrl"] = metadata.LogoutUrl;
            }

            EnsureEmailClaimMappers(representation, identityProvider.Alias);
        });
    }

    private static void EnsureEmailClaimMappers(RealmRepresentation representation, string identityProviderAlias)
    {
        ArgumentNullException.ThrowIfNull(representation);
        ArgumentException.ThrowIfNullOrWhiteSpace(identityProviderAlias);

        representation.IdentityProviderMappers ??= [];

        UpsertUserAttributeMapper(
            representation.IdentityProviderMappers,
            identityProviderAlias,
            name: "email",
            claim: "email");
        UpsertUserAttributeMapper(
            representation.IdentityProviderMappers,
            identityProviderAlias,
            name: "preferred_username-email",
            claim: "preferred_username");
    }

    private static void UpsertUserAttributeMapper(
        ConcurrentBag<IdentityProviderMapperRepresentation> mappers,
        string identityProviderAlias,
        string name,
        string claim)
    {
        var mapper = mappers.GetOrAdd(
            existing =>
                string.Equals(existing.Name, name, StringComparison.Ordinal)
                && string.Equals(existing.IdentityProviderAlias, identityProviderAlias, StringComparison.Ordinal),
            new IdentityProviderMapperRepresentation
            {
                Name = name,
                IdentityProviderAlias = identityProviderAlias,
                IdentityProviderMapper = "oidc-user-attribute-idp-mapper",
            });

        mapper.IdentityProviderAlias = identityProviderAlias;
        mapper.IdentityProviderMapper = "oidc-user-attribute-idp-mapper";
        mapper.Config = new ConcurrentDictionary<string, string>(StringComparer.Ordinal)
        {
            ["syncMode"] = "INHERIT",
            ["claim"] = claim,
            ["user.attribute"] = "email",
        };
    }

    private static async Task<OidcDiscoveryMetadata> FetchOidcDiscoveryAsync(
        string discoveryEndpoint,
        CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(discoveryEndpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;

        static string Require(JsonElement root, string name, string discoveryEndpoint)
        {
            if (root.TryGetProperty(name, out var property) && property.GetString() is { Length: > 0 } value)
            {
                return value;
            }

            throw new InvalidOperationException(
                $"OIDC discovery document at '{discoveryEndpoint}' is missing required property '{name}'.");
        }

        return new OidcDiscoveryMetadata(
            AuthorizationUrl: Require(root, "authorization_endpoint", discoveryEndpoint),
            TokenUrl: Require(root, "token_endpoint", discoveryEndpoint),
            JwksUrl: Require(root, "jwks_uri", discoveryEndpoint),
            Issuer: Require(root, "issuer", discoveryEndpoint),
            UserInfoUrl: root.TryGetProperty("userinfo_endpoint", out var userInfo)
                ? userInfo.GetString()
                : null,
            LogoutUrl: root.TryGetProperty("end_session_endpoint", out var endSession)
                ? endSession.GetString()
                : null);
    }

    private sealed record OidcDiscoveryMetadata(
        string AuthorizationUrl,
        string TokenUrl,
        string JwksUrl,
        string Issuer,
        string? UserInfoUrl,
        string? LogoutUrl);
}
