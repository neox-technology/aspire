using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Creates an Entra password credential via Graph and persists the one-shot secret into AppHost state.
/// </summary>
internal static class EntraClientSecretCreator
{
    internal const string NameInputName = "secret-name";
    internal const string LifetimeInputName = "secret-lifetime";

#pragma warning disable ASPIREINTERACTION001
    /// <summary>
    /// Prompts for secret display name + lifetime, calls Graph <c>addPassword</c>, and persists the value.
    /// </summary>
    public static async Task<(bool Success, string Message)> CreateInteractiveAsync(
        EntraAuthAppRegistrationResource app,
        IServiceProvider services,
        string? applicationObjectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(services);

        if (!app.Annotations.OfType<ClientSecretAnnotation>().Any())
        {
            return (false, $"Auth app '{app.Name}' does not use WithClientSecret.");
        }

        var interaction = services.GetService<IInteractionService>();
        if (interaction is null || !interaction.IsAvailable)
        {
            return (false, "Interaction service is unavailable. Set Parameters__* for the client secret in CI.");
        }

        if (!AuthParameterResolution.TryGetResolvedValue(app.ClientIdParameter, services, out var clientId)
            || string.IsNullOrWhiteSpace(clientId)
            || EntraAppRegistrationParameterPrompt.IsCreateSentinel(clientId))
        {
            return (false, $"Auth app '{app.Name}' has no resolved ClientId yet.");
        }

        var provisioner = services.GetService<IEntraGraphAppProvisioner>()
            ?? EntraGraphAppProvisioner.Create(services);

        var objectId = applicationObjectId;
        if (string.IsNullOrWhiteSpace(objectId))
        {
            objectId = await provisioner.TryGetApplicationObjectIdAsync(clientId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(objectId))
        {
            return (false, $"No Entra application found with client id '{clientId}'.");
        }

        var nameInput = new InteractionInput
        {
            Name = NameInputName,
            InputType = InputType.Text,
            Label = "Secret name",
            Placeholder = "neox-aspire",
            Required = true
        };
        var lifetimeInput = new InteractionInput
        {
            Name = LifetimeInputName,
            InputType = InputType.Choice,
            Label = "Lifetime",
            Required = true,
            Options = EntraClientSecretLifetime.ChoiceOptions,
            Value = EntraClientSecretLifetime.DefaultKey
        };

        var inputsResult = await interaction.PromptInputsAsync(
                title: $"Create client secret — {app.Name}",
                message: $"Create an Entra password credential for '{app.DisplayName}' and save it to AppHost secrets.",
                [nameInput, lifetimeInput],
                options: null,
                cancellationToken)
            .ConfigureAwait(false);

        if (inputsResult.Canceled || inputsResult.Data is null)
        {
            return (false, "Client secret creation was canceled.");
        }

        var displayName = inputsResult.Data.GetString(NameInputName)?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (false, "Client secret creation was canceled.");
        }

        var lifetimeKey = inputsResult.Data.GetString(LifetimeInputName);
        var endDateTime = EntraClientSecretLifetime.ResolveEndDateTime(lifetimeKey, DateTimeOffset.UtcNow);

        var secretText = await provisioner
            .AddPasswordCredentialAsync(objectId, displayName, endDateTime, cancellationToken)
            .ConfigureAwait(false);

        await AuthParameterValue.SetAsync(
                services,
                app.ClientSecretParameter,
                secretText,
                cancellationToken)
            .ConfigureAwait(false);

        var logger = services.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(EntraClientSecretCreator));
        logger?.LogInformation(
            "Created Entra client secret '{DisplayName}' (expires {EndDateTime:u}) for Auth app '{App}' and persisted it to parameter '{Parameter}'.",
            displayName,
            endDateTime,
            app.Name,
            app.ClientSecretParameter.Name);

        return (true, $"Client secret '{displayName}' created (expires {endDateTime:u}) and saved to AppHost secrets.");
    }
#pragma warning restore ASPIREINTERACTION001
}
