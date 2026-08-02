using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Dashboard command to create an Entra client secret on <see cref="EntraClientSecretResource"/>.
/// </summary>
internal static class EntraAuthClientSecretCommandExtensions
{
    public const string CreateClientSecretCommandName = "create-client-secret";

    public static IResourceBuilder<EntraClientSecretResource> WithCreateClientSecretCommand(
        this IResourceBuilder<EntraClientSecretResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Any(a => string.Equals(a.Name, CreateClientSecretCommandName, StringComparison.Ordinal)))
        {
            return builder;
        }

        var secretResource = builder.Resource;
        var app = secretResource.Owner;
        var commandOptions = new CommandOptions
        {
            IconName = "Key",
            IconVariant = IconVariant.Filled,
            UpdateState = context =>
            {
                if (!AuthParameterResolution.TryGetResolvedValue(
                        app.ClientIdParameter,
                        context.ServiceProvider,
                        out var clientId)
                    || string.IsNullOrWhiteSpace(clientId)
                    || EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId))
                {
                    return ResourceCommandState.Disabled;
                }

                if (AuthParameterResolution.TryGetResolvedValue(
                        secretResource.Parameter,
                        context.ServiceProvider,
                        out var secret)
                    && !string.IsNullOrWhiteSpace(secret))
                {
                    return ResourceCommandState.Disabled;
                }

                return ResourceCommandState.Enabled;
            }
        };

        return builder.WithCommand(
            name: CreateClientSecretCommandName,
            displayName: "Create client secret",
            executeCommand: context => ExecuteCreateAsync(secretResource, context),
            commandOptions: commandOptions);
    }

    private static async Task<ExecuteCommandResult> ExecuteCreateAsync(
        EntraClientSecretResource secretResource,
        ExecuteCommandContext context)
    {
        var app = secretResource.Owner;
        var logger = context.ServiceProvider.GetService<ResourceLoggerService>()?.GetLogger(secretResource)
            ?? context.ServiceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(EntraAuthClientSecretCommandExtensions));

        try
        {
            var (success, message) = await EntraClientSecretCreator.CreateInteractiveAsync(
                    app,
                    context.ServiceProvider,
                    applicationObjectId: null,
                    context.CancellationToken)
                .ConfigureAwait(false);

            var statusService = context.ServiceProvider.GetService<EntraAuthDashboardStatusService>();
            if (statusService is not null)
            {
                await statusService.RefreshAsync(context.ServiceProvider, context.CancellationToken)
                    .ConfigureAwait(false);
            }

            return success ? CommandResults.Success(message) : CommandResults.Failure(message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogError(ex, "Create client secret failed for Auth app '{App}'.", app.Name);
            return CommandResults.Failure(ex.Message);
        }
    }
}
