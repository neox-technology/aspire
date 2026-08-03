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

                return ResourceCommandState.Enabled;
            }
        };

        return builder.WithCommand(
            name: ProvisionCommandName,
            displayName: "Provision app registration",
            executeCommand: context => ExecuteProvisionAsync(app, context),
            commandOptions: commandOptions);
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
}
