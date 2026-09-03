using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Provisioning;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Primitives;
using Neox.Azure.Provisioning.Graph;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire Azure resource that provisions or references a Microsoft Entra ID app registration
/// via Microsoft Graph constructs compiled with Azure.Provisioning.
/// </summary>
public sealed class AzureEntraIdAppRegistrationResource : AzureProvisioningResource, IResourceWithWaitSupport
{
    internal const string ClientIdOutputName = "clientId";
    internal const string ObjectIdOutputName = "objectId";
    internal const string UniqueNameParameter = "uniqueName";
    internal const string DisplayNameParameter = "displayName";
    internal const string RedirectUrisParameter = "redirectUris";
    internal const string SpaRedirectUrisParameter = "spaRedirectUris";
    internal const string IdentifierUrisParameter = "identifierUris";

    internal const string GraphApplicationBicepIdentifier = "app";
    internal const string GraphApplicationIdentifierUrisBicepIdentifier = "appIdentifierUris";
    internal const string GraphServicePrincipalBicepIdentifier = "sp";
    internal const string GraphScopeAccessType = "Scope";
    internal const string GraphRoleAccessType = "Role";

    internal const string GraphBicepExtensionVersion = "1.0.0";

    internal static readonly string BicepConfigJson =
        $$"""
        {
          "experimentalFeaturesEnabled": {
            "extensibility": true
          },
          "extensions": {
            "microsoftGraphV1": "br:mcr.microsoft.com/bicep/extensions/microsoftgraph/v1.0:{{GraphBicepExtensionVersion}}"
          }
        }
        """;

    /// <summary>
    /// Initializes a new <see cref="AzureEntraIdAppRegistrationResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name (also the default Graph <c>uniqueName</c>).</param>
    public AzureEntraIdAppRegistrationResource(string name)
        : base(name, ConfigureGraphInfrastructure)
    {
    }

    /// <summary>
    /// Application (client) ID output from the Graph application.
    /// </summary>
    public BicepOutputReference ClientId => new(ClientIdOutputName, this);

    /// <summary>
    /// Directory object ID output from the Graph application.
    /// </summary>
    public BicepOutputReference ObjectId => new(ObjectIdOutputName, this);

    /// <summary>
    /// OAuth2 permission scopes registered via <c>AddScope</c>. Emitted on the create Graph application only.
    /// </summary>
    internal List<AzureEntraIdScopeResource> Scopes { get; } = [];

    /// <summary>
    /// App roles registered via <c>AddAppRole</c>. Emitted on the create Graph application only.
    /// </summary>
    internal List<AzureEntraIdAppRoleResource> AppRoles { get; } = [];

    /// <summary>
    /// In-model API permissions registered via <c>WithPermission</c>. Emitted as
    /// Graph <c>requiredResourceAccess</c> on the create Graph application only.
    /// </summary>
    internal List<IAzureEntraIdPermissibleResource> RequiredPermissions { get; } = [];

    /// <summary>
    /// Store certificates registered via <c>WithKeyCredential</c>. Emitted as Graph
    /// <c>keyCredentials</c> on the create Graph application only.
    /// </summary>
    internal List<AsymmetricX509CertResource> KeyCredentials { get; } = [];

    /// <summary>
    /// Password credentials registered via <c>WithSecret</c>. Created post-provision via Graph
    /// <c>addPassword</c> on each <see cref="EntraIdPasswordCredentialResource"/> (not Bicep).
    /// </summary>
    internal List<EntraIdPasswordCredentialResource> PasswordCredentials { get; } = [];

    /// <summary>
    /// Web platforms registered via <c>AddWebApplication</c>. Emitted as Graph
    /// <c>web.redirectUris</c> on the create Graph application only.
    /// </summary>
    internal List<AzureEntraIdWebApplicationResource> WebApplications { get; } = [];

    /// <summary>
    /// SPA platforms registered via <c>AddSpaApplication</c>. Emitted as Graph
    /// <c>spa.redirectUris</c> on the create Graph application only.
    /// </summary>
    internal List<AzureEntraIdSpaApplicationResource> SpaApplications { get; } = [];

    /// <inheritdoc />
    public override BicepTemplateFile GetBicepTemplateFile(string? directory = null, bool deleteTemporaryFileOnDispose = true)
    {
        ApplyExistingUniqueNameParameter();
        StripCreateOnlyParametersIfExisting();

        var file = base.GetBicepTemplateFile(directory, deleteTemporaryFileOnDispose);
        var dir = Path.GetDirectoryName(file.Path)
            ?? throw new InvalidOperationException("Generated Bicep module path has no directory.");

        File.WriteAllText(Path.Combine(dir, "bicepconfig.json"), BicepConfigJson);
        PrependGraphExtension(file.Path);
        return file;
    }

    /// <inheritdoc />
    public override ProvisionableResource AddAsExistingResource(AzureResourceInfrastructure infra)
    {
        var existing = infra.GetProvisionableResources()
            .OfType<GraphApplication>()
            .SingleOrDefault(app => app.BicepIdentifier == GraphApplicationBicepIdentifier);

        if (existing is not null)
        {
            return existing;
        }

        var app = GraphApplication.FromExisting(GraphApplicationBicepIdentifier, ResolveExistingUniqueName(infra));
        infra.Add(app);
        return app;
    }

    private static void ConfigureGraphInfrastructure(AzureResourceInfrastructure infrastructure)
    {
        var azureResource = (AzureEntraIdAppRegistrationResource)infrastructure.AspireResource;
        azureResource.ApplyExistingUniqueNameParameter();
        azureResource.StripCreateOnlyParametersIfExisting();

        var app = CreateExistingOrNewProvisionableResource(
            infrastructure,
            (_, uniqueName) => GraphApplication.FromExisting(GraphApplicationBicepIdentifier, uniqueName),
            infra =>
            {
                var created = new GraphApplication(GraphApplicationBicepIdentifier)
                {
                    UniqueName = AsStringParameter(azureResource, infra, UniqueNameParameter),
                    DisplayName = AsStringParameter(azureResource, infra, DisplayNameParameter),
                    SignInAudience = ResolveSignInAudience(azureResource),
                    Web = BindWebApplication(azureResource, infra)
                };

                BindSpaApplication(azureResource, infra, created);
                BindExplicitIdentifierUris(azureResource, infra, created);
                BindApi(azureResource, created);
                BindAppRoles(azureResource, created);
                BindRequiredResourceAccess(azureResource, infra, created);
                BindKeyCredentials(azureResource, infra, created);
                return created;
            });

        BindDefaultIdentifierUriResource(azureResource, infrastructure, app);

        var sp = new GraphServicePrincipal(GraphServicePrincipalBicepIdentifier)
        {
            AppId = app.AppId
        };
        infrastructure.Add(sp);

        infrastructure.Add(new ProvisioningOutput(ClientIdOutputName, typeof(string))
        {
            Value = app.AppId
        });
        infrastructure.Add(new ProvisioningOutput(ObjectIdOutputName, typeof(string))
        {
            Value = app.Id
        });
    }

    private BicepValue<string> ResolveExistingUniqueName(AzureResourceInfrastructure infra)
    {
        if (!this.TryGetLastAnnotation<ExistingAzureResourceAnnotation>(out var existing))
        {
            return Name;
        }

        return existing.Name switch
        {
            ParameterResource p => p.AsProvisioningParameter(infra, UniqueNameParameter),
            BicepOutputReference output => output.AsProvisioningParameter(infra, UniqueNameParameter),
            string s => s,
            _ => Name
        };
    }

    private void ApplyExistingUniqueNameParameter()
    {
        if (!this.TryGetLastAnnotation<ExistingAzureResourceAnnotation>(out var existing))
        {
            return;
        }

        Parameters[UniqueNameParameter] = existing.Name switch
        {
            string s => s,
            ParameterResource p => p,
            BicepOutputReference output => output,
            _ => Parameters.GetValueOrDefault(UniqueNameParameter) ?? Name
        };
    }

    private void StripCreateOnlyParametersIfExisting()
    {
        if (!this.IsExisting())
        {
            return;
        }

        Parameters.Remove(DisplayNameParameter);
        Parameters.Remove(RedirectUrisParameter);
        Parameters.Remove(SpaRedirectUrisParameter);
        Parameters.Remove(IdentifierUrisParameter);
        foreach (var cert in KeyCredentials)
        {
            Parameters.Remove(AsymmetricX509CertResource.KeyCredentialParameterName(cert.Name));
        }
    }

    private static string ResolveSignInAudience(AzureEntraIdAppRegistrationResource resource)
    {
        if (resource.TryGetLastAnnotation<AzureEntraIdSupportedAccountTypeAnnotation>(out var annotation))
        {
            return annotation.SupportedAccountType switch
            {
                SupportedAccountType.AzureADMyOrg => "AzureADMyOrg",
                SupportedAccountType.AzureADMultipleOrgs => "AzureADMultipleOrgs",
                SupportedAccountType.AzureADandPersonalMicrosoftAccount => "AzureADandPersonalMicrosoftAccount",
                SupportedAccountType.PersonalMicrosoftAccount => "PersonalMicrosoftAccount",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(annotation.SupportedAccountType),
                    annotation.SupportedAccountType,
                    null)
            };
        }

        return "AzureADMyOrg";
    }

    private static bool RequiresAccessTokenVersion2(AzureEntraIdAppRegistrationResource resource)
    {
        var audience = ResolveSignInAudience(resource);
        return audience is "AzureADandPersonalMicrosoftAccount" or "PersonalMicrosoftAccount";
    }

    private static BicepValue<string> AsStringParameter(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure,
        string parameterName)
    {
        if (resource.Parameters.TryGetValue(parameterName, out var value))
        {
            switch (value)
            {
                case ParameterResource p:
                    return p.AsProvisioningParameter(infrastructure, parameterName);
                case BicepOutputReference output:
                    return output.AsProvisioningParameter(infrastructure, parameterName);
                case IManifestExpressionProvider provider:
                    return provider.AsProvisioningParameter(infrastructure, parameterName);
            }
        }

        return GetOrAddParameter(infrastructure, parameterName, typeof(string));
    }

    private static void BindExplicitIdentifierUris(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure,
        GraphApplication created)
    {
        var useDefault = resource.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _);
        var explicitUris = resource.TryGetLastAnnotation<AzureEntraIdIdentifierUrisAnnotation>(out var annotation)
            ? annotation.IdentifierUris
            : [];

        if (useDefault || explicitUris.Count == 0)
        {
            return;
        }

        created.IdentifierUris = BindListParameter(infrastructure, IdentifierUrisParameter);
    }

    private static void BindApi(
        AzureEntraIdAppRegistrationResource resource,
        GraphApplication created)
    {
        if (!RequiresAccessTokenVersion2(resource) && resource.Scopes.Count == 0)
        {
            return;
        }

        var api = new GraphApiApplication
        {
            RequestedAccessTokenVersion = 2
        };
        foreach (var scope in resource.Scopes)
        {
            var permission = new GraphPermissionScope
            {
                Id = scope.Id.ToString("D"),
                Value = scope.Value,
                AdminConsentDisplayName = scope.AdminConsentDisplayName,
                AdminConsentDescription = scope.AdminConsentDescription,
                IsEnabled = true,
                Type = scope.ConsentType
            };

            if (scope.UserConsentDisplayName is not null && scope.UserConsentDescription is not null)
            {
                permission.UserConsentDisplayName = scope.UserConsentDisplayName;
                permission.UserConsentDescription = scope.UserConsentDescription;
            }

            api.Oauth2PermissionScopes.Add(permission);
        }

        created.Api = api;
    }

    private static void BindAppRoles(
        AzureEntraIdAppRegistrationResource resource,
        GraphApplication created)
    {
        if (resource.AppRoles.Count == 0)
        {
            return;
        }

        foreach (var appRole in resource.AppRoles)
        {
            var role = new GraphAppRole
            {
                Id = appRole.Id.ToString("D"),
                Value = appRole.Value,
                DisplayName = appRole.DisplayName,
                Description = appRole.Description,
                IsEnabled = true
            };

            foreach (var memberType in appRole.GraphAllowedMemberTypes)
            {
                role.AllowedMemberTypes.Add(memberType);
            }

            created.AppRoles.Add(role);
        }
    }

    internal static string ResourceAppIdParameterName(string exposerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(exposerName);
        return "resourceAppId_" + exposerName.Replace("-", "_", StringComparison.Ordinal);
    }

    private static void BindRequiredResourceAccess(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure,
        GraphApplication created)
    {
        if (resource.RequiredPermissions.Count == 0)
        {
            return;
        }

        foreach (var group in resource.RequiredPermissions.GroupBy(permission => permission.Parent))
        {
            var access = new GraphRequiredResourceAccess();
            if (ReferenceEquals(group.Key, resource))
            {
                access.ResourceAppId = created.AppId;
            }
            else
            {
                access.ResourceAppId = AsStringParameter(
                    resource,
                    infrastructure,
                    ResourceAppIdParameterName(group.Key.Name));
            }

            foreach (var permission in group)
            {
                access.ResourceAccess.Add(new GraphResourceAccess
                {
                    Id = permission.Id.ToString("D"),
                    Type = permission.Type == AzureEntraIdPermissionType.Scope
                        ? GraphScopeAccessType
                        : GraphRoleAccessType
                });
            }

            created.RequiredResourceAccess.Add(access);
        }
    }

    private static void BindKeyCredentials(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure,
        GraphApplication created)
    {
        if (resource.KeyCredentials.Count == 0)
        {
            return;
        }

        foreach (var cert in resource.KeyCredentials)
        {
            created.KeyCredentials.Add(new GraphKeyCredential
            {
                DisplayName = cert.Name,
                Type = "AsymmetricX509Cert",
                Usage = "Verify",
                Key = AsSecureStringParameter(
                    infrastructure,
                    AsymmetricX509CertResource.KeyCredentialParameterName(cert.Name))
            });
        }
    }

    private static void BindDefaultIdentifierUriResource(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure,
        GraphApplication app)
    {
        if (resource.IsExisting())
        {
            return;
        }

        if (!resource.TryGetLastAnnotation<AzureEntraIdDefaultIdentifierUriAnnotation>(out _))
        {
            return;
        }

        var explicitUris = resource.TryGetLastAnnotation<AzureEntraIdIdentifierUrisAnnotation>(out var annotation)
            ? annotation.IdentifierUris
            : [];

        var list = new BicepList<string>
        {
            BicepFunction.Interpolate($"api://{app.AppId}")
        };
        foreach (var uri in explicitUris)
        {
            list.Add(uri);
        }

        infrastructure.Add(new GraphApplication(GraphApplicationIdentifierUrisBicepIdentifier)
        {
            UniqueName = app.UniqueName,
            DisplayName = app.DisplayName,
            SignInAudience = ResolveSignInAudience(resource),
            IdentifierUris = list,
            Api = new GraphApiApplication
            {
                RequestedAccessTokenVersion = 2
            }
        });
    }

    internal string[] CollectRedirectUris()
    {
        var uris = new List<string>();
        if (this.TryGetLastAnnotation<AzureEntraIdRedirectUrisAnnotation>(out var annotation))
        {
            foreach (var uri in annotation.RedirectUris)
            {
                if (!uris.Contains(uri, StringComparer.Ordinal))
                {
                    uris.Add(uri);
                }
            }
        }

        foreach (var web in WebApplications)
        {
            foreach (var uri in web.RedirectUris)
            {
                var value = uri.AbsoluteUri;
                if (!uris.Contains(value, StringComparer.Ordinal))
                {
                    uris.Add(value);
                }
            }
        }

        return [.. uris];
    }

    internal bool HasWebRedirectUris() => CollectRedirectUris().Length > 0;

    internal string[] CollectSpaRedirectUris()
    {
        var uris = new List<string>();
        foreach (var spa in SpaApplications)
        {
            foreach (var uri in spa.RedirectUris)
            {
                var value = uri.AbsoluteUri;
                if (!uris.Contains(value, StringComparer.Ordinal))
                {
                    uris.Add(value);
                }
            }
        }

        return [.. uris];
    }

    internal bool HasSpaRedirectUris() => CollectSpaRedirectUris().Length > 0;

    private static void BindSpaApplication(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure,
        GraphApplication created)
    {
        if (!resource.HasSpaRedirectUris())
        {
            return;
        }

        created.Spa = new GraphSpaApplication
        {
            RedirectUris = BindListParameter(infrastructure, SpaRedirectUrisParameter)
        };
    }

    private static GraphWebApplication BindWebApplication(
        AzureEntraIdAppRegistrationResource resource,
        AzureResourceInfrastructure infrastructure)
    {
        var web = new GraphWebApplication
        {
            RedirectUris = BindListParameter(infrastructure, RedirectUrisParameter)
        };

        if (resource.HasWebRedirectUris())
        {
            web.ImplicitGrantSettings = new GraphImplicitGrantSettings
            {
                EnableAccessTokenIssuance = true,
                EnableIdTokenIssuance = true
            };
        }

        return web;
    }

    private static BicepList<string> BindListParameter(
        AzureResourceInfrastructure infrastructure,
        string parameterName)
    {
        var parameter = GetOrAddParameter(infrastructure, parameterName, typeof(object[]));
        var list = new BicepList<string>();
        ((IBicepValue)list).Expression = new IdentifierExpression(parameter.BicepIdentifier);
        return list;
    }

    private static BicepValue<string> AsSecureStringParameter(
        AzureResourceInfrastructure infrastructure,
        string parameterName)
    {
        var parameter = GetOrAddParameter(infrastructure, parameterName, typeof(string));
        parameter.IsSecure = true;
        return parameter;
    }

    private static ProvisioningParameter GetOrAddParameter(
        AzureResourceInfrastructure infrastructure,
        string parameterName,
        Type type)
    {
        var parameter = new ProvisioningParameter(parameterName, type);
        infrastructure.Add(parameter);
        return parameter;
    }

    private static void PrependGraphExtension(string modulePath)
    {
        var bicep = File.ReadAllText(modulePath);
        if (bicep.Contains("extension microsoftGraphV1", StringComparison.Ordinal))
        {
            return;
        }

        File.WriteAllText(modulePath, "extension microsoftGraphV1" + Environment.NewLine + Environment.NewLine + bicep);
    }
}
