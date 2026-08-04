using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard commands for Entra app registrations.
/// </summary>
internal static class EntraAuthAppRegistrationCommandExtensions
{
    public const string ProvisionCommandName = "provision-auth";
    public const string SelectAppRegistrationCommandName = "select-app-registration";

    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithSelectAppRegistrationCommand(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Any(a => string.Equals(a.Name, SelectAppRegistrationCommandName, StringComparison.Ordinal)))
        {
            return builder;
        }

        var app = builder.Resource;
        var commandOptions = new CommandOptions
        {
            IconName = "Apps",
            IconVariant = IconVariant.Filled,
            UpdateState = context => EvaluateSelectAppState(app, context.ServiceProvider)
        };

        return builder.WithCommand(
            name: SelectAppRegistrationCommandName,
            displayName: "Select or create app registration",
            executeCommand: context => ExecuteSelectAppAsync(app, context),
            commandOptions: commandOptions);
    }

    public static IResourceBuilder<EntraAuthAppRegistrationResource> WithProvisionAuthCommand(
        this IResourceBuilder<EntraAuthAppRegistrationResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Any(a => string.Equals(a.Name, ProvisionCommandName, StringComparison.Ordinal)))
        {
            return builder;
        }

        var app = builder.Resource;
        var commandOptions = new CommandOptions
        {
            IconName = "CloudSync",
            IconVariant = IconVariant.Filled,
            UpdateState = context =>
            {
                var statusService = context.ServiceProvider.GetService<EntraAuthDashboardStatusService>();
                if (statusService?.IsProvisioning(app.Name) == true)
                {
                    return ResourceCommandState.Disabled;
                }

                if (statusService?.TryGetAppStatus(app.Name, out var status) == true
                    && status == AuthDashboardStatus.Waiting)
                {
                    return ResourceCommandState.Disabled;
                }

                if (!AuthParameterResolution.TryGetResolvedValue(
                        app.TenantIdParameter,
                        context.ServiceProvider,
                        out var tenantId)
                    || string.IsNullOrWhiteSpace(tenantId))
                {
                    return ResourceCommandState.Disabled;
                }

                var isExplicitCreate = EntraAuthCommandEnablement.IsExplicitCreateSentinel(
                    app.ClientIdParameter,
                    context.ServiceProvider);

                if (statusService?.TryGetAppStatus(app.Name, out var status) == true
                    && status == AuthDashboardStatus.Waiting
                    && !isExplicitCreate)
                {
                    return ResourceCommandState.Disabled;
                }

                return ResourceCommandState.Enabled;
            }
        };

        return builder.WithCommand(
            name: ProvisionCommandName,
            displayName: "Provision app registration",
            executeCommand: context => ExecuteProvisionAsync(app, context),
            commandOptions: commandOptions);
    }

    internal static ResourceCommandState EvaluateSelectAppState(
        EntraAuthAppRegistrationResource app,
        IServiceProvider services)
    {
        var statusService = services.GetService<EntraAuthDashboardStatusService>();
        if (statusService?.IsProvisioning(app.Name) == true)
        {
            return ResourceCommandState.Disabled;
        }

        if (!AuthParameterResolution.TryGetResolvedValue(
                app.TenantIdParameter,
                services,
                out var tenantId)
            || string.IsNullOrWhiteSpace(tenantId))
        {
            return ResourceCommandState.Disabled;
        }

        if (!EntraAuthCommandEnablement.IsClientIdUnsetOrCreateSentinel(app.ClientIdParameter, services))
        {
            return ResourceCommandState.Disabled;
        }

        if (!EntraAuthCommandEnablement.AreWaitForAuthDependenciesHealthy(app, statusService))
        {
            return ResourceCommandState.Disabled;
        }

        return ResourceCommandState.Enabled;
    }

#pragma warning disable ASPIREINTERACTION001
    private static async Task<ExecuteCommandResult> ExecuteSelectAppAsync(
        EntraAuthAppRegistrationResource app,
        ExecuteCommandContext context)
    {
        var services = context.ServiceProvider;
        var logger = services.GetService<ResourceLoggerService>()?.GetLogger(app)
            ?? services.GetService<ILoggerFactory>()?.CreateLogger(typeof(EntraAuthAppRegistrationCommandExtensions));

        try
        {
            if (EvaluateSelectAppState(app, services) != ResourceCommandState.Enabled)
            {
                return CommandResults.Failure(
                    $"Select app registration is not available for '{app.Name}' (tenant unset, ClientId already set, WaitFor deps not Healthy, or provisioning).");
            }

            var interaction = services.GetService<IInteractionService>();
            if (interaction is null || !interaction.IsAvailable)
            {
                return CommandResults.Failure(
                    "Interaction service is unavailable. Set Parameters__* for the ClientId in CI.");
            }

            if (!AuthParameterResolution.TryGetResolvedValue(
                    app.TenantIdParameter,
                    services,
                    out var tenantId)
                || string.IsNullOrWhiteSpace(tenantId))
            {
                return CommandResults.Failure($"Tenant is not set for Auth app '{app.Name}'.");
            }

            var apps = await EntraAppRegistrationEnumerator
                .TryGetAppRegistrationOptionsAsync(tenantId!, cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);

            var providerName = app.Parent.Name;
            var input = new InteractionInput
            {
                Name = app.ClientIdParameter.Name,
                InputType = InputType.Choice,
                Label = EntraAppRegistrationParameterPrompt.FormatLabel(app.Name, app.DisplayName),
                Description = EntraAppRegistrationParameterPrompt.FormatDescription(
                    apps.Count > 0,
                    providerName,
                    app.Name,
                    app.ClientIdParameter.Name),
                Required = true,
                AllowCustomChoice = true,
                Options = EntraAppRegistrationParameterPrompt.BuildOptions(app.DisplayName, apps)
            };

            var result = await interaction.PromptInputsAsync(
                    title: $"Select app registration — {app.Name}",
                    message: $"Choose an existing Entra app or Create for '{app.DisplayName}'.",
                    [input],
                    options: null,
                    context.CancellationToken)
                .ConfigureAwait(false);

            if (result.Canceled || result.Data is null)
            {
                return CommandResults.Canceled();
            }

            var clientId = result.Data.GetString(app.ClientIdParameter.Name)?.Trim();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return CommandResults.Canceled();
            }

            var isCreate = string.Equals(
                clientId,
                EntraAppRegistrationParameterPrompt.CreateSentinel,
                StringComparison.Ordinal);

            await AuthParameterValue.SetAsync(
                    services,
                    app.ClientIdParameter,
                    clientId,
                    context.CancellationToken,
                    persistToDeploymentState: !isCreate)
                .ConfigureAwait(false);

            var statusService = services.GetService<EntraAuthDashboardStatusService>();
            if (statusService is not null)
            {
                await statusService.RefreshAsync(services, context.CancellationToken).ConfigureAwait(false);
            }

            logger?.LogInformation(
                "Selected Entra app registration for Auth app '{App}' (create={Create}).",
                app.Name,
                isCreate);

            return CommandResults.Success(
                isCreate
                    ? $"Create selected for '{app.Name}'. Run Provision app registration next."
                    : $"ClientId selected for '{app.Name}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Select app registration failed for Auth app '{App}'.", app.Name);
            return CommandResults.Failure(ex.Message);
        }
    }

    private static async Task<ExecuteCommandResult> ExecuteProvisionAsync(
        EntraAuthAppRegistrationResource app,
        ExecuteCommandContext context)
    {
        var services = context.ServiceProvider;
        var statusService = services.GetRequiredService<EntraAuthDashboardStatusService>();
        var logger = services.GetService<ResourceLoggerService>()?.GetLogger(app)
            ?? services.GetService<ILoggerFactory>()?.CreateLogger(typeof(EntraAuthAppRegistrationCommandExtensions));

        if (statusService.IsProvisioning(app.Name))
        {
            return CommandResults.Failure("Provisioning is already in progress.");
        }

        statusService.SetProvisioning(app.Name, true);
        try
        {
            logger?.LogInformation("Dashboard provision started for Auth app '{App}'.", app.Name);

            var provisioner = services.GetService<IEntraGraphAppProvisioner>()
                ?? EntraGraphAppProvisioner.Create(services);

            await AuthParameterPrompt.EnsureReadyAsync(
                    services,
                    [app.TenantIdParameter],
                    context.CancellationToken)
                .ConfigureAwait(false);

            var plan = await provisioner.PlanAsync(app, context.CancellationToken).ConfigureAwait(false);

            var interaction = services.GetService<IInteractionService>();
            if (interaction is null || !interaction.IsAvailable)
            {
                return CommandResults.Failure(
                    "Interaction service is unavailable. Confirm provision is only supported from the Aspire dashboard.");
            }

            var confirmation = await interaction.PromptConfirmationAsync(
                    title: EntraAuthProvisionConfirmation.BuildTitle(app),
                    message: EntraAuthProvisionConfirmation.BuildMessage(app, plan),
                    options: new MessageBoxInteractionOptions
                    {
                        Intent = MessageIntent.Confirmation,
                        EnableMessageMarkdown = true
                    },
                    cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);

            if (confirmation.Canceled || confirmation.Data != true)
            {
                logger?.LogInformation("Dashboard provision canceled for Auth app '{App}'.", app.Name);
                return CommandResults.Canceled();
            }

            var result = await provisioner.ProvisionAsync(app, plan, context.CancellationToken)
                .ConfigureAwait(false);

            await AuthParameterValue.SetAsync(
                    services,
                    app.TenantIdParameter,
                    result.TenantId,
                    context.CancellationToken)
                .ConfigureAwait(false);
            await AuthParameterValue.SetAsync(
                    services,
                    app.ClientIdParameter,
                    result.ClientId,
                    context.CancellationToken)
                .ConfigureAwait(false);

            await statusService.RefreshAsync(services, context.CancellationToken).ConfigureAwait(false);

            logger?.LogInformation(
                "Dashboard provision completed for Auth app '{App}' (ClientId={ClientId}).",
                app.Name,
                result.ClientId);

            return CommandResults.Success($"Provisioned app registration '{app.Name}' ({result.ClientId}).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Dashboard provision failed for Auth app '{App}'.", app.Name);
            try
            {
                await statusService.RefreshAsync(services, context.CancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort refresh after failure.
            }

            return CommandResults.Failure(ex.Message);
        }
        finally
        {
            statusService.SetProvisioning(app.Name, false);
        }
    }
#pragma warning restore ASPIREINTERACTION001
}
