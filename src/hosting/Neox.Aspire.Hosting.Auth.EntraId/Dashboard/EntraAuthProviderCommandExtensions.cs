using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard command to select the Entra tenant on <see cref="EntraAuthOpsResource"/>.
/// </summary>
internal static class EntraAuthProviderCommandExtensions
{
    public const string SelectTenantCommandName = "select-tenant";

    public static IResourceBuilder<EntraAuthOpsResource> WithSelectTenantCommand(
        this IResourceBuilder<EntraAuthOpsResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Any(a => string.Equals(a.Name, SelectTenantCommandName, StringComparison.Ordinal)))
        {
            return builder;
        }

        var provider = builder.Resource;
        var commandOptions = new CommandOptions
        {
            IconName = "Building",
            IconVariant = IconVariant.Filled,
            UpdateState = context =>
            {
                if (AuthParameterResolution.TryGetResolvedValue(
                        provider.TenantIdParameter,
                        context.ServiceProvider,
                        out var tenantId)
                    && !string.IsNullOrWhiteSpace(tenantId))
                {
                    return ResourceCommandState.Disabled;
                }

                return ResourceCommandState.Enabled;
            }
        };

        return builder.WithCommand(
            name: SelectTenantCommandName,
            displayName: "Select tenant",
            executeCommand: context => ExecuteSelectTenantAsync(provider, context),
            commandOptions: commandOptions);
    }

#pragma warning disable ASPIREINTERACTION001
    private static async Task<ExecuteCommandResult> ExecuteSelectTenantAsync(
        EntraAuthOpsResource provider,
        ExecuteCommandContext context)
    {
        var services = context.ServiceProvider;
        var logger = services.GetService<ResourceLoggerService>()?.GetLogger(provider)
            ?? services.GetService<ILoggerFactory>()?.CreateLogger(typeof(EntraAuthProviderCommandExtensions));

        try
        {
            if (AuthParameterResolution.TryGetResolvedValue(
                    provider.TenantIdParameter,
                    services,
                    out var existing)
                && !string.IsNullOrWhiteSpace(existing))
            {
                return CommandResults.Failure($"Tenant is already set for provider '{provider.Name}'.");
            }

            var interaction = services.GetService<IInteractionService>();
            if (interaction is null || !interaction.IsAvailable)
            {
                return CommandResults.Failure(
                    "Interaction service is unavailable. Set Parameters__* for the tenant id in CI.");
            }

            var tenants = await EntraTenantEnumerator
                .TryGetTenantOptionsAsync(cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);

            var input = new InteractionInput
            {
                Name = provider.TenantIdParameter.Name,
                InputType = tenants.Count > 0 ? InputType.Choice : InputType.Text,
                Label = EntraTenantParameterPrompt.FormatLabel(provider.Name),
                Description = EntraTenantParameterPrompt.FormatDescription(
                    tenants.Count > 0,
                    provider.Name,
                    provider.TenantIdParameter.Name),
                Required = true,
                AllowCustomChoice = tenants.Count > 0,
                Options = tenants.Count > 0 ? tenants : null
            };

            var result = await interaction.PromptInputsAsync(
                    title: $"Select tenant — {provider.Name}",
                    message: $"Choose the Entra tenant for Auth provider '{provider.Name}'.",
                    [input],
                    options: null,
                    context.CancellationToken)
                .ConfigureAwait(false);

            if (result.Canceled || result.Data is null)
            {
                return CommandResults.Canceled();
            }

            var tenantId = result.Data.GetString(provider.TenantIdParameter.Name)?.Trim();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return CommandResults.Canceled();
            }

            await AuthParameterValue.SetAsync(
                    services,
                    provider.TenantIdParameter,
                    tenantId,
                    context.CancellationToken)
                .ConfigureAwait(false);

            var statusService = services.GetService<EntraAuthDashboardStatusService>();
            if (statusService is not null)
            {
                await statusService.RefreshAsync(services, context.CancellationToken).ConfigureAwait(false);
            }

            logger?.LogInformation(
                "Selected Entra tenant for provider '{Provider}' (parameter '{Parameter}').",
                provider.Name,
                provider.TenantIdParameter.Name);

            return CommandResults.Success($"Tenant selected for '{provider.Name}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Select tenant failed for provider '{Provider}'.", provider.Name);
            return CommandResults.Failure(ex.Message);
        }
    }
#pragma warning restore ASPIREINTERACTION001
}
