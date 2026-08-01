#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra-specific AuthOps pipeline registration and step names.
/// </summary>
public static class EntraAuthOpsExtensions
{
    /// <summary>Deploy gate required by Aspire <c>deploy</c> (<c>deploy-auth</c>).</summary>
    public const string AuthDeployStepName = "deploy-auth";

    /// <summary>
    /// Builds <c>prereq-{providerResource}-auth</c> for an Entra provider resource name.
    /// </summary>
    public static string GetPrereqStepName(string providerResourceName) =>
        AuthOpsExtensions.GetPrereqProviderAuthStepName(providerResourceName);

    internal static void EnsurePrereqEntraStep(
        IDistributedApplicationBuilder applicationBuilder,
        IResourceBuilder<EntraAuthOpsResource> provider)
    {
        AuthOpsExtensions.EnsurePrereqProvidersAuthGate(applicationBuilder);

        var stepName = GetPrereqStepName(provider.Resource.Name);
        if (provider.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, stepName, StringComparison.Ordinal)))
        {
            return;
        }

        provider.WithAnnotation(new AuthNamedStepAnnotation(stepName));
        provider.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = stepName,
            Description = "AuthOps Entra prerequisite: tenant parameter and Graph credential path.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
            Action = async context =>
            {
                var entra = provider.Resource;
                await EntraTenantParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    entra.TenantIdParameter,
                    context.CancellationToken).ConfigureAwait(false);
            }
        });
    }

    internal static void EnsureDeployAuthGate(IResourceBuilder<EntraAuthOpsResource> provider)
    {
        if (provider.Resource.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, AuthDeployStepName, StringComparison.Ordinal)))
        {
            return;
        }

        provider.WithAnnotation(new AuthNamedStepAnnotation(AuthDeployStepName));
        provider.WithPipelineStepFactory(factoryContext => new PipelineStep
        {
            Name = AuthDeployStepName,
            Description = "AuthOps deploy gate — all Auth app provision steps completed.",
            Tags = ["auth-ops"],
            Resource = factoryContext.Resource,
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Action = _ => Task.CompletedTask
        });
    }

    internal static void RegisterAppPipelineSteps(
        IResourceBuilder<EntraAuthOpsResource> provider,
        IResourceBuilder<AuthAppResource> appBuilder)
    {
        var app = appBuilder.Resource;
        var prereqName = AuthOpsExtensions.GetPrereqAppAuthStepName(app.Name);
        var planName = AuthOpsExtensions.GetPlanAuthStepName(app.Name);
        var provisionName = AuthOpsExtensions.GetProvisionAuthStepName(app.Name);

        if (app.Annotations.OfType<AuthNamedStepAnnotation>()
            .Any(a => string.Equals(a.StepName, planName, StringComparison.Ordinal)))
        {
            return;
        }

        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(prereqName));
        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(planName));
        appBuilder.WithAnnotation(new AuthNamedStepAnnotation(provisionName));

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = prereqName,
            Description =
                $"AuthOps prerequisite: ClientId for app '{app.Name}' (adopt GUID or create with DisplayName '{app.Options.DisplayName}').",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [AuthOpsExtensions.AuthPrereqProvidersStepName],
            Action = async context =>
            {
                await EntraAppRegistrationParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    app.ClientIdParameter,
                    app.TenantIdParameter,
                    app.Options.DisplayName,
                    context.CancellationToken).ConfigureAwait(false);
            }
        });

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = planName,
            Description = $"Validate AuthOps desired model for app '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [prereqName],
            Action = async context =>
            {
                await ValidateAppOptionsAsync(app, context.CancellationToken).ConfigureAwait(false);
            }
        });

        appBuilder.WithPipelineStepFactory(_ => new PipelineStep
        {
            Name = provisionName,
            Description = $"Provision or adopt Entra app registration '{app.Name}'.",
            Tags = ["auth-ops"],
            Resource = app,
            DependsOnSteps = [planName],
            RequiredBySteps = [AuthDeployStepName],
            Action = async context =>
            {
                var provisioner = context.Services.GetService(typeof(IEntraGraphAppProvisioner)) as IEntraGraphAppProvisioner
                    ?? EntraGraphAppProvisioner.Create(context.Services);

                await AuthParameterPrompt.EnsureReadyAsync(
                    context.Services,
                    CollectProvisionInputs(app),
                    context.CancellationToken).ConfigureAwait(false);

                var result = await provisioner.ProvisionAsync(app, context.CancellationToken).ConfigureAwait(false);

                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.TenantIdParameter,
                    result.TenantId,
                    context.CancellationToken).ConfigureAwait(false);
                await AuthParameterValue.SetAsync(
                    context.Services,
                    app.ClientIdParameter,
                    result.ClientId,
                    context.CancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(result.ClientSecret))
                {
                    await AuthParameterValue.SetAsync(
                        context.Services,
                        app.ClientSecretParameter,
                        result.ClientSecret,
                        context.CancellationToken).ConfigureAwait(false);
                }
                else if (app.Options.CreateClientSecret || app.Options.RotateClientSecret)
                {
                    await AuthParameterPrompt.EnsureReadyAsync(
                        context.Services,
                        [app.ClientSecretParameter],
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
        });

        EnsureDeployAuthGate(provider);
    }

    private static IEnumerable<ParameterResource> CollectProvisionInputs(AuthAppResource app)
    {
        yield return app.TenantIdParameter;
    }

    private static async Task ValidateAppOptionsAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        var o = app.Options;
        if (string.IsNullOrWhiteSpace(o.DisplayName))
        {
            throw new InvalidOperationException($"Auth app '{app.Name}' requires DisplayName.");
        }

        var clientId = await TryGetClientIdAsync(app, cancellationToken).ConfigureAwait(false);
        var isCreate = EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId)
            && string.IsNullOrWhiteSpace(o.ExistingClientId);

        if (o.ApplicationType is AuthApplicationType.Web or AuthApplicationType.Spa or AuthApplicationType.Native
            && o.RedirectUris.Count == 0
            && isCreate)
        {
            throw new InvalidOperationException(
                $"Auth app '{app.Name}' ({o.ApplicationType}) requires at least one RedirectUri when creating.");
        }
    }

    private static Task<string?> TryGetClientIdAsync(AuthAppResource app, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (app.ClientIdParameter is null)
        {
            return Task.FromResult<string?>(null);
        }

        // Prefer non-blocking read — plan runs after prereq resolved ClientId when interactive.
        if (TryPeekParameterValue(app.ClientIdParameter, out var peeked))
        {
            return Task.FromResult(peeked);
        }

        return Task.FromResult<string?>(null);
    }

    private static bool TryPeekParameterValue(ParameterResource parameter, out string? value)
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
                if (taskProp?.GetValue(tcsObj) is Task<string> { IsCompletedSuccessfully: true } task)
                {
                    value = task.Result;
                    return true;
                }

                if (taskProp?.GetValue(tcsObj) is Task { IsCompleted: false })
                {
                    return false;
                }
            }

            value = parameter.GetValueAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
