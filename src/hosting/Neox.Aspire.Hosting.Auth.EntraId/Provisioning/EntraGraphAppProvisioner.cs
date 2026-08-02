using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Microsoft.Graph.Applications.Item.AddPassword;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Microsoft Graph implementation of <see cref="IEntraGraphAppProvisioner"/>.
/// </summary>
public sealed class EntraGraphAppProvisioner : IEntraGraphAppProvisioner
{
    private const string NeoxMarkerExtension = "neoxAspireAuthApp";

    private readonly GraphServiceClient _graph;
    private readonly IConfiguration? _configuration;

    public EntraGraphAppProvisioner(GraphServiceClient graph, IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graph = graph;
        _configuration = configuration;
    }

    public static EntraGraphAppProvisioner Create(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configuration = services.GetService<IConfiguration>();
        var credential = services.GetService<ITokenCredentialProvider>()?.TokenCredential
            ?? new DefaultAzureCredential();

        var graph = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        return new EntraGraphAppProvisioner(graph, configuration);
    }

    public async Task<AuthAppRegistrationPlan> PlanAsync(EntraAuthAppRegistrationResource app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Provider is not EntraAuthOpsResource entra)
        {
            throw new InvalidOperationException(
                $"Auth app '{app.Name}' provider '{app.Provider.Name}' is not Entra.");
        }

        if (string.IsNullOrWhiteSpace(app.DisplayName))
        {
            throw new InvalidOperationException($"Auth app '{app.Name}' requires DisplayName.");
        }

        var tenantId = await ResolveTenantIdAsync(entra, app, cancellationToken).ConfigureAwait(false);
        var desiredRedirects = await EntraRedirectUriApplicator.ResolveAsync(app, cancellationToken)
            .ConfigureAwait(false);
        var desiredSignInAudience = SupportedAccountsMapping.GetDesiredSignInAudience(app);
        var desiredIdentifierUris = EntraApiExpositionApplicator.CollectDesiredIdentifierUris(app);
        var desiredScopes = EntraApiExpositionApplicator.CollectDesiredScopes(app);
        var desiredAppRoles = EntraApiExpositionApplicator.CollectDesiredAppRoles(app);
        var desiredPermissions = EntraApiPermissionApplicator.CollectDesired(app, TryResolveClientId);
        var adoptClientId = ResolveAdoptClientId(app);

        if (!string.IsNullOrWhiteSpace(adoptClientId))
        {
            var existing = await GetByAppIdAsync(adoptClientId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"No Entra application found with client id '{adoptClientId}'.");

            return BuildAdoptPlan(
                tenantId,
                app.DisplayName,
                desiredSignInAudience,
                existing,
                desiredRedirects,
                desiredIdentifierUris,
                desiredScopes,
                desiredAppRoles,
                desiredPermissions,
                EntraApiPermissionApplicator.HasDeclaredPermissions(app));
        }

        var byName = await FindByDisplayNameAsync(app.DisplayName, cancellationToken).ConfigureAwait(false);
        if (byName is not null)
        {
            return BuildAdoptPlan(
                tenantId,
                app.DisplayName,
                desiredSignInAudience,
                byName,
                desiredRedirects,
                desiredIdentifierUris,
                desiredScopes,
                desiredAppRoles,
                desiredPermissions,
                EntraApiPermissionApplicator.HasDeclaredPermissions(app));
        }

        var createActions = new List<AuthAppRegistrationPlanAction>
        {
            AuthAppRegistrationPlanAction.CreateApplication
        };
        if (desiredIdentifierUris.Count > 0)
        {
            createActions.Add(AuthAppRegistrationPlanAction.UpdateIdentifierUris);
        }

        if (desiredScopes.Count > 0)
        {
            createActions.Add(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes);
        }

        if (desiredAppRoles.Count > 0)
        {
            createActions.Add(AuthAppRegistrationPlanAction.UpdateAppRoles);
        }

        if (EntraApiPermissionApplicator.HasDeclaredPermissions(app))
        {
            createActions.Add(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess);
        }

        return new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Create,
            TenantId = tenantId,
            DesiredDisplayName = app.DisplayName,
            DesiredSignInAudience = desiredSignInAudience,
            DesiredRedirectUris = desiredRedirects,
            DesiredIdentifierUris = desiredIdentifierUris,
            DesiredScopes = desiredScopes,
            DesiredAppRoles = desiredAppRoles,
            DesiredRequiredResourceAccess = desiredPermissions,
            Existing = null,
            Actions = createActions
        };
    }

    public async Task<EntraProvisionResult> ProvisionAsync(
        EntraAuthAppRegistrationResource app,
        AuthAppRegistrationPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.Actions.Contains(AuthAppRegistrationPlanAction.CreateApplication))
        {
            var created = await CreateApplicationAsync(
                    plan.DesiredDisplayName,
                    plan.DesiredSignInAudience,
                    plan.DesiredRedirectUris,
                    cancellationToken)
                .ConfigureAwait(false);

            var clientId = created.AppId
                ?? throw new InvalidOperationException("Created application missing AppId.");
            var objectId = created.Id
                ?? throw new InvalidOperationException("Created application missing object id.");

            await ApplyExpositionAndPermissionsAsync(
                    app,
                    plan,
                    objectId,
                    clientId,
                    existingIdentifierUris: [],
                    existingScopes: [],
                    existingAppRoles: [],
                    existingPermissions: [],
                    cancellationToken)
                .ConfigureAwait(false);

            return new EntraProvisionResult
            {
                TenantId = plan.TenantId,
                ClientId = clientId,
                ClientSecret = null,
                ApplicationObjectId = objectId
            };
        }

        var existing = plan.Existing
            ?? throw new InvalidOperationException(
                $"Auth app '{app.Name}' plan is Adopt but has no existing snapshot.");

        if (plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateDisplayName)
            || plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateRedirectUris)
            || plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateSignInAudience))
        {
            await PatchApplicationAsync(
                    existing.ObjectId,
                    plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateDisplayName)
                        ? plan.DesiredDisplayName
                        : null,
                    plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateSignInAudience)
                        ? plan.DesiredSignInAudience
                        : null,
                    plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateRedirectUris)
                        ? plan.DesiredRedirectUris
                        : null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await ApplyExpositionAndPermissionsAsync(
                app,
                plan,
                existing.ObjectId,
                existing.AppId,
                existing.IdentifierUris,
                existing.Scopes,
                existing.AppRoles,
                existing.RequiredResourceAccess,
                cancellationToken)
            .ConfigureAwait(false);

        return new EntraProvisionResult
        {
            TenantId = plan.TenantId,
            ClientId = existing.AppId,
            ClientSecret = null,
            ApplicationObjectId = existing.ObjectId
        };
    }

    public async Task<string> AddPasswordCredentialAsync(
        string applicationObjectId,
        string displayName,
        DateTimeOffset endDateTime,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationObjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        try
        {
            var created = await _graph.Applications[applicationObjectId].AddPassword
                .PostAsync(
                    new AddPasswordPostRequestBody
                    {
                        PasswordCredential = new PasswordCredential
                        {
                            DisplayName = displayName.Trim(),
                            EndDateTime = endDateTime.UtcDateTime
                        }
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var secretText = created?.SecretText;
            if (string.IsNullOrWhiteSpace(secretText))
            {
                throw new InvalidOperationException(
                    $"Graph addPassword for application '{applicationObjectId}' returned no secretText.");
            }

            return secretText;
        }
        catch (ODataError ex)
        {
            throw new InvalidOperationException(
                $"Failed to create client secret on Entra application '{applicationObjectId}': {ex.Error?.Message ?? ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Resolves the Graph application object id for a client (app) id.
    /// </summary>
    public async Task<string?> TryGetApplicationObjectIdAsync(string clientId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        var application = await GetByAppIdAsync(clientId, cancellationToken).ConfigureAwait(false);
        return application?.Id;
    }

    internal static AuthAppRegistrationPlan BuildAdoptPlan(
        string tenantId,
        string desiredDisplayName,
        string desiredSignInAudience,
        Application existing,
        IReadOnlyList<AuthDesiredRedirectUri> desiredRedirects,
        IReadOnlyList<AuthDesiredIdentifierUri>? desiredIdentifierUris = null,
        IReadOnlyList<AuthDesiredOauth2PermissionScope>? desiredScopes = null,
        IReadOnlyList<AuthDesiredAppRole>? desiredAppRoles = null,
        IReadOnlyList<AuthDesiredRequiredResourceAccess>? desiredPermissions = null,
        bool hasDeclaredPermissions = false)
    {
        desiredIdentifierUris ??= [];
        desiredScopes ??= [];
        desiredAppRoles ??= [];
        desiredPermissions ??= [];

        var objectId = existing.Id
            ?? throw new InvalidOperationException("Graph application missing object id.");
        var appId = existing.AppId
            ?? throw new InvalidOperationException("Graph application missing AppId.");

        var existingRedirects = EntraRedirectUriApplicator.Extract(existing);
        var existingIdentifierUris = EntraApiExpositionApplicator.ExtractIdentifierUris(existing);
        var existingScopes = EntraApiExpositionApplicator.ExtractScopes(existing);
        var existingAppRoles = EntraApiExpositionApplicator.ExtractAppRoles(existing);
        var existingPermissions = EntraApiPermissionApplicator.Extract(existing);

        var remappedScopes = RemapScopeIds(desiredScopes, existingScopes);
        var remappedRoles = RemapAppRoleIds(desiredAppRoles, existingAppRoles);

        var snapshot = new AuthAppRegistrationExistingSnapshot
        {
            ObjectId = objectId,
            AppId = appId,
            DisplayName = existing.DisplayName,
            SignInAudience = existing.SignInAudience,
            RedirectUris = existingRedirects,
            IdentifierUris = existingIdentifierUris,
            Scopes = existingScopes,
            AppRoles = existingAppRoles,
            RequiredResourceAccess = existingPermissions
        };

        var actions = new List<AuthAppRegistrationPlanAction>();
        if (!string.Equals(existing.DisplayName, desiredDisplayName, StringComparison.Ordinal))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateDisplayName);
        }

        if (!string.Equals(existing.SignInAudience, desiredSignInAudience, StringComparison.Ordinal))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateSignInAudience);
        }

        if (EntraRedirectUriApplicator.Differ(desiredRedirects, existingRedirects))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateRedirectUris);
        }

        if (EntraApiExpositionApplicator.IdentifierUrisDiffer(
                desiredIdentifierUris, existingIdentifierUris, appId))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateIdentifierUris);
        }

        if (EntraApiExpositionApplicator.ScopesDiffer(remappedScopes, existingScopes))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes);
        }

        if (EntraApiExpositionApplicator.AppRolesDiffer(remappedRoles, existingAppRoles))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateAppRoles);
        }

        if (hasDeclaredPermissions
            && (desiredPermissions.Count == 0
                || EntraApiPermissionApplicator.Differ(desiredPermissions, existingPermissions)))
        {
            // desiredPermissions empty means exposer ClientId not yet resolvable — still plan update.
            actions.Add(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess);
        }
        else if (EntraApiPermissionApplicator.Differ(desiredPermissions, existingPermissions))
        {
            actions.Add(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess);
        }

        if (actions.Count == 0)
        {
            actions.Add(AuthAppRegistrationPlanAction.None);
        }

        return new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Adopt,
            TenantId = tenantId,
            DesiredDisplayName = desiredDisplayName,
            DesiredSignInAudience = desiredSignInAudience,
            DesiredRedirectUris = desiredRedirects,
            DesiredIdentifierUris = desiredIdentifierUris,
            DesiredScopes = remappedScopes,
            DesiredAppRoles = remappedRoles,
            DesiredRequiredResourceAccess = desiredPermissions,
            Existing = snapshot,
            Actions = actions
        };
    }

    /// <summary>
    /// Back-compat overload used by SupportedAccountsTests.
    /// </summary>
    internal static AuthAppRegistrationPlan BuildAdoptPlan(
        string tenantId,
        string desiredDisplayName,
        string desiredSignInAudience,
        Application existing,
        IReadOnlyList<AuthDesiredRedirectUri> desiredRedirects) =>
        BuildAdoptPlan(
            tenantId,
            desiredDisplayName,
            desiredSignInAudience,
            existing,
            desiredRedirects,
            desiredIdentifierUris: null,
            desiredScopes: null,
            desiredAppRoles: null,
            desiredPermissions: null,
            hasDeclaredPermissions: false);

    private async Task ApplyExpositionAndPermissionsAsync(
        EntraAuthAppRegistrationResource app,
        AuthAppRegistrationPlan plan,
        string objectId,
        string clientId,
        IReadOnlyList<AuthDesiredIdentifierUri> existingIdentifierUris,
        IReadOnlyList<AuthDesiredOauth2PermissionScope> existingScopes,
        IReadOnlyList<AuthDesiredAppRole> existingAppRoles,
        IReadOnlyList<AuthDesiredRequiredResourceAccess> existingPermissions,
        CancellationToken cancellationToken)
    {
        var patch = new Application();
        var needsPatch = false;

        if (plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateIdentifierUris)
            && plan.DesiredIdentifierUris.Count > 0)
        {
            var resolved = EntraApiExpositionApplicator.ResolveIdentifierUris(
                plan.DesiredIdentifierUris, clientId);
            var merged = EntraApiExpositionApplicator.MergeIdentifierUrisForApply(
                resolved, existingIdentifierUris);
            EntraApiExpositionApplicator.ApplyIdentifierUris(patch, merged);
            needsPatch = true;
        }

        if (plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateOauth2PermissionScopes)
            && plan.DesiredScopes.Count > 0)
        {
            var merged = EntraApiExpositionApplicator.MergeScopesForApply(
                plan.DesiredScopes, existingScopes);
            EntraApiExpositionApplicator.ApplyScopes(patch, merged);
            needsPatch = true;
        }

        if (plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateAppRoles)
            && plan.DesiredAppRoles.Count > 0)
        {
            var merged = EntraApiExpositionApplicator.MergeAppRolesForApply(
                plan.DesiredAppRoles, existingAppRoles);
            EntraApiExpositionApplicator.ApplyAppRoles(patch, merged);
            needsPatch = true;
        }

        if (plan.Actions.Contains(AuthAppRegistrationPlanAction.UpdateRequiredResourceAccess)
            && EntraApiPermissionApplicator.HasDeclaredPermissions(app))
        {
            // Re-collect after exposer provision (DependsOn) so ClientId is available.
            var desired = EntraApiPermissionApplicator.CollectDesired(app, TryResolveClientId);
            if (desired.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Auth app '{app.Name}' has WithApiPermission but exposer ClientId is not resolved yet. " +
                    "Ensure provision-{exposer}-auth runs before this step.");
            }

            // Remap permission ids from exposer plan when adopt reused Graph ids.
            desired = RemapPermissionIdsFromExposerPlans(app, desired);

            var merged = EntraApiPermissionApplicator.MergeForApply(desired, existingPermissions);
            EntraApiPermissionApplicator.Apply(patch, merged);
            needsPatch = true;
        }

        if (needsPatch)
        {
            await _graph.Applications[objectId].PatchAsync(patch, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<AuthDesiredRequiredResourceAccess> RemapPermissionIdsFromExposerPlans(
        EntraAuthAppRegistrationResource app,
        IReadOnlyList<AuthDesiredRequiredResourceAccess> desired)
    {
        var result = new List<AuthDesiredRequiredResourceAccess>();
        foreach (var annotation in app.Annotations.OfType<ApiPermissionAnnotation>())
        {
            var exposition = annotation.Exposition;
            var exposerPlan = exposition.Owner.Annotations
                .OfType<AuthAppRegistrationPlanAnnotation>()
                .LastOrDefault()
                ?.Plan;

            Guid permissionId;
            string type;
            switch (exposition)
            {
                case ScopeApiExposition scope:
                    type = "Scope";
                    permissionId = exposerPlan?.DesiredScopes
                        .FirstOrDefault(s => string.Equals(s.Value, scope.ScopeValue, StringComparison.Ordinal))
                        ?.Id
                        ?? scope.PermissionId;
                    break;
                case AppRoleApiExposition role:
                    type = "Role";
                    permissionId = exposerPlan?.DesiredAppRoles
                        .FirstOrDefault(r => string.Equals(r.Value, role.Value, StringComparison.Ordinal))
                        ?.Id
                        ?? role.RoleId;
                    break;
                default:
                    continue;
            }

            var resourceAppId = TryResolveClientId(exposition.Owner);
            if (string.IsNullOrWhiteSpace(resourceAppId))
            {
                continue;
            }

            result.Add(new AuthDesiredRequiredResourceAccess
            {
                ResourceAppId = resourceAppId,
                PermissionId = permissionId,
                Type = type
            });
        }

        return result.Count > 0 ? result : desired;
    }

    private static IReadOnlyList<AuthDesiredOauth2PermissionScope> RemapScopeIds(
        IReadOnlyList<AuthDesiredOauth2PermissionScope> desired,
        IReadOnlyList<AuthDesiredOauth2PermissionScope> existing)
    {
        var byValue = existing.ToDictionary(s => s.Value, StringComparer.Ordinal);
        return desired.Select(d =>
            byValue.TryGetValue(d.Value, out var ex) && ex.Id != Guid.Empty
                ? d with { Id = ex.Id }
                : d).ToList();
    }

    private static IReadOnlyList<AuthDesiredAppRole> RemapAppRoleIds(
        IReadOnlyList<AuthDesiredAppRole> desired,
        IReadOnlyList<AuthDesiredAppRole> existing)
    {
        var byValue = existing.ToDictionary(r => r.Value, StringComparer.Ordinal);
        return desired.Select(d =>
            byValue.TryGetValue(d.Value, out var ex) && ex.Id != Guid.Empty
                ? d with { Id = ex.Id }
                : d).ToList();
    }

    private static string? TryResolveClientId(EntraAuthAppRegistrationResource app)
    {
        if (app.ClientIdParameter is not null
            && TryGetResolvedParameterValue(app.ClientIdParameter, out var fromParam)
            && !string.IsNullOrWhiteSpace(fromParam)
            && !EntraAppRegistrationParameterPrompt.IsCreateSentinel(fromParam)
            && Guid.TryParse(fromParam, out _))
        {
            return fromParam;
        }

        var plan = app.Annotations.OfType<AuthAppRegistrationPlanAnnotation>().LastOrDefault()?.Plan;
        if (plan?.Existing?.AppId is { } existingAppId)
        {
            return existingAppId;
        }

        return null;
    }

    private static string? ResolveAdoptClientId(EntraAuthAppRegistrationResource app)
    {
        if (app.ClientIdParameter is not null
            && TryGetResolvedParameterValue(app.ClientIdParameter, out var fromParam))
        {
            if (!EntraAppRegistrationParameterPrompt.IsCreateSentinel(fromParam)
                && Guid.TryParse(fromParam, out _))
            {
                return fromParam;
            }

            if (!string.IsNullOrWhiteSpace(fromParam)
                && !EntraAppRegistrationParameterPrompt.IsCreateSentinel(fromParam)
                && !Guid.TryParse(fromParam, out _))
            {
                throw new InvalidOperationException(
                    $"Auth app '{app.Name}' ClientId '{fromParam}' is not a valid GUID. " +
                    "Provide an existing application (client) id or choose Create.");
            }
        }

        return null;
    }

    /// <summary>
    /// Reads a parameter value only when already resolved (never blocks on <c>WaitForValueTcs</c>).
    /// </summary>
    private static bool TryGetResolvedParameterValue(ParameterResource parameter, out string? value)
    {
        value = null;
        try
        {
            var prop = typeof(ParameterResource).GetProperty(
                "WaitForValueTcs",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            var tcsObj = prop?.GetValue(parameter);
            if (tcsObj is not null)
            {
                var taskProp = tcsObj.GetType().GetProperty("Task");
                if (taskProp?.GetValue(tcsObj) is Task<string> task)
                {
                    if (!task.IsCompletedSuccessfully)
                    {
                        return false;
                    }

                    value = task.Result;
                    return true;
                }

                if (taskProp?.GetValue(tcsObj) is Task { IsCompleted: false })
                {
                    return false;
                }
            }

            value = parameter.GetValueAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ResolveTenantIdAsync(
        EntraAuthOpsResource entra,
        EntraAuthAppRegistrationResource app,
        CancellationToken cancellationToken)
    {
        _ = entra;

        if (app.TenantIdParameter is not null)
        {
            var fromParam = await app.TenantIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fromParam))
            {
                return fromParam;
            }
        }

        var fromConfig = FirstNonEmpty(
            _configuration?["Azure:TenantId"],
            Environment.GetEnvironmentVariable("Azure__TenantId"),
            Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));

        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        throw new InvalidOperationException(
            "Entra tenant id is required. Set Entra options TenantId parameter, Parameters__{provider}-tenant-id, or Azure__TenantId.");
    }

    private async Task<Application?> GetByAppIdAsync(string clientId, CancellationToken cancellationToken)
    {
        try
        {
            var page = await _graph.Applications.GetAsync(request =>
            {
                request.QueryParameters.Filter = $"appId eq '{EscapeOData(clientId)}'";
                request.QueryParameters.Top = 1;
            }, cancellationToken).ConfigureAwait(false);

            return page?.Value?.FirstOrDefault();
        }
        catch (ODataError ex)
        {
            throw new InvalidOperationException(
                $"Failed to look up Entra application '{clientId}': {ex.Error?.Message ?? ex.Message}", ex);
        }
    }

    private async Task<Application?> FindByDisplayNameAsync(string displayName, CancellationToken cancellationToken)
    {
        var page = await _graph.Applications.GetAsync(request =>
        {
            request.QueryParameters.Filter = $"displayName eq '{EscapeOData(displayName)}'";
            request.QueryParameters.Top = 5;
        }, cancellationToken).ConfigureAwait(false);

        return page?.Value?.FirstOrDefault();
    }

    private async Task<Application> CreateApplicationAsync(
        string displayName,
        string signInAudience,
        IReadOnlyList<AuthDesiredRedirectUri> redirectUris,
        CancellationToken cancellationToken)
    {
        var application = new Application
        {
            DisplayName = displayName,
            SignInAudience = signInAudience,
            Notes = $"neox-aspire-auth:{NeoxMarkerExtension}"
        };
        EntraRedirectUriApplicator.Apply(application, redirectUris);

        var created = await _graph.Applications.PostAsync(application, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return created ?? throw new InvalidOperationException("Graph Applications.PostAsync returned null.");
    }

    private async Task PatchApplicationAsync(
        string objectId,
        string? displayName,
        string? signInAudience,
        IReadOnlyList<AuthDesiredRedirectUri>? redirectUris,
        CancellationToken cancellationToken)
    {
        var patch = new Application();
        if (displayName is not null)
        {
            patch.DisplayName = displayName;
        }

        if (signInAudience is not null)
        {
            patch.SignInAudience = signInAudience;
        }

        if (redirectUris is not null)
        {
            EntraRedirectUriApplicator.Apply(patch, redirectUris);
        }

        await _graph.Applications[objectId].PatchAsync(patch, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
