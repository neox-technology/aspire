using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
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

    public async Task<AuthAppRegistrationPlan> PlanAsync(AuthAppResource app, CancellationToken cancellationToken)
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
        var adoptClientId = ResolveAdoptClientId(app);

        if (!string.IsNullOrWhiteSpace(adoptClientId))
        {
            var existing = await GetByAppIdAsync(adoptClientId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"No Entra application found with client id '{adoptClientId}'.");

            return BuildAdoptPlan(tenantId, app.DisplayName, desiredSignInAudience, existing, desiredRedirects);
        }

        var byName = await FindByDisplayNameAsync(app.DisplayName, cancellationToken).ConfigureAwait(false);
        if (byName is not null)
        {
            return BuildAdoptPlan(tenantId, app.DisplayName, desiredSignInAudience, byName, desiredRedirects);
        }

        return new AuthAppRegistrationPlan
        {
            Mode = AuthAppRegistrationPlanMode.Create,
            TenantId = tenantId,
            DesiredDisplayName = app.DisplayName,
            DesiredSignInAudience = desiredSignInAudience,
            DesiredRedirectUris = desiredRedirects,
            Existing = null,
            Actions = [AuthAppRegistrationPlanAction.CreateApplication]
        };
    }

    public async Task<EntraProvisionResult> ProvisionAsync(
        AuthAppResource app,
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

            return new EntraProvisionResult
            {
                TenantId = plan.TenantId,
                ClientId = created.AppId ?? throw new InvalidOperationException("Created application missing AppId."),
                ClientSecret = null,
                ApplicationObjectId = created.Id
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

        return new EntraProvisionResult
        {
            TenantId = plan.TenantId,
            ClientId = existing.AppId,
            ClientSecret = null,
            ApplicationObjectId = existing.ObjectId
        };
    }

    internal static AuthAppRegistrationPlan BuildAdoptPlan(
        string tenantId,
        string desiredDisplayName,
        string desiredSignInAudience,
        Application existing,
        IReadOnlyList<AuthDesiredRedirectUri> desiredRedirects)
    {
        var objectId = existing.Id
            ?? throw new InvalidOperationException("Graph application missing object id.");
        var appId = existing.AppId
            ?? throw new InvalidOperationException("Graph application missing AppId.");

        var existingRedirects = EntraRedirectUriApplicator.Extract(existing);
        var snapshot = new AuthAppRegistrationExistingSnapshot
        {
            ObjectId = objectId,
            AppId = appId,
            DisplayName = existing.DisplayName,
            SignInAudience = existing.SignInAudience,
            RedirectUris = existingRedirects
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
            Existing = snapshot,
            Actions = actions
        };
    }

    private static string? ResolveAdoptClientId(AuthAppResource app)
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
        AuthAppResource app,
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
