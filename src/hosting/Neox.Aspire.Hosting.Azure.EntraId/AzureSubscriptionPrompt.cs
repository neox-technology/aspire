#pragma warning disable ASPIREAZURE001
#pragma warning disable ASPIREINTERACTION001
#pragma warning disable ASPIREPIPELINES002

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Opens Aspire's Azure subscription dialog when an Entra app registration is the
/// only resource that needs a provisioning context.
/// </summary>
internal static class AzureSubscriptionPrompt {

    private const string ChangeAzureContextCommandName = "change-azure-context";

    public static async Task EnsureOpenAsync(
        IServiceProvider services,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(services);

        var interaction = services.GetService<IInteractionService>();

        if (interaction is null || !interaction.IsAvailable) {
            return;
        }

        if (await HasContextAsync(services, cancellationToken).ConfigureAwait(false)) {
            return;
        }

        var model = services.GetRequiredService<DistributedApplicationModel>();
        var environment = model.Resources.OfType<AzureEnvironmentResource>().SingleOrDefault();

        if (environment is null) {
            return;
        }

        var annotation = environment.Annotations
            .OfType<ResourceCommandAnnotation>()
            .SingleOrDefault(command => command.Name == ChangeAzureContextCommandName);

        if (annotation is null) {
            return;
        }

        var result = await ExecuteChangeContextAsync(services, environment, annotation, cancellationToken).ConfigureAwait(false);

        if (IsOperationBusy(result)) {
            var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            _ = Task.Run(() => RetryWhenCommandEnabledAsync(services, environment, annotation, stopping), CancellationToken.None);
        }
    }

    private static async Task<ExecuteCommandResult> ExecuteChangeContextAsync(
        IServiceProvider services,
        AzureEnvironmentResource environment,
        ResourceCommandAnnotation annotation,
        CancellationToken cancellationToken) {
        return await annotation.ExecuteCommand(new ExecuteCommandContext {
            ResourceName = environment.Name,
            Services = services,
            CancellationToken = cancellationToken,
            Logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(AzureSubscriptionPrompt)),
            Arguments = new InteractionInputCollection([])
        }).ConfigureAwait(false);
    }

    private static async Task<bool> HasContextAsync(
        IServiceProvider services,
        CancellationToken cancellationToken) {
        var configuration = services.GetRequiredService<IConfiguration>();
        var state = services.GetRequiredService<IDeploymentStateManager>();

        var section = await state.AcquireSectionAsync("Azure", cancellationToken).ConfigureAwait(false);

        var subscription = section.Data["SubscriptionId"]?.GetValue<string>()
            ?? configuration["Azure:SubscriptionId"];
        var location = section.Data["Location"]?.GetValue<string>()
            ?? configuration["Azure:Location"];

        return !string.IsNullOrEmpty(subscription) && !string.IsNullOrEmpty(location);
    }

    private static bool IsOperationBusy(ExecuteCommandResult result)
        => !result.Success && result.Message?.Contains("already running or queued", StringComparison.Ordinal) == true;

    private static async Task RetryWhenCommandEnabledAsync(
        IServiceProvider services,
        AzureEnvironmentResource environment,
        ResourceCommandAnnotation annotation,
        CancellationToken cancellationToken) {
        var notifications = services.GetRequiredService<ResourceNotificationService>();
        var waitingForIdle = false;
        var attemptedWhileEnabled = false;

        await foreach (var resourceEvent in notifications.WatchAsync(cancellationToken).ConfigureAwait(false)) {
            if (!string.Equals(resourceEvent.Resource.Name, environment.Name, StringComparison.Ordinal)) {
                continue;
            }

            var command = resourceEvent.Snapshot.Commands.FirstOrDefault(candidate => candidate.Name == ChangeAzureContextCommandName);
            var enabled = command?.State == ResourceCommandState.Enabled;

            if (command is null || !enabled) {
                waitingForIdle = true;
                attemptedWhileEnabled = false;
                continue;
            }

            if (!waitingForIdle && attemptedWhileEnabled) {
                continue;
            }

            waitingForIdle = false;
            attemptedWhileEnabled = true;
            var result = await ExecuteChangeContextAsync(services, environment, annotation, cancellationToken).ConfigureAwait(false);

            if (!IsOperationBusy(result)) {
                return;
            }
        }
    }
}
