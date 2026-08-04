#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Prompts for an Auth app ClientId: adopt an existing app (listed from the tenant) or choose create (sentinel).
/// DisplayName for create comes from <c>AddAppRegistration</c> (not an Aspire parameter).
/// </summary>
internal static class EntraAppRegistrationParameterPrompt
{
    /// <summary>
    /// Choice option key meaning "create a new Entra application" (not a real ClientId).
    /// </summary>
    public const string CreateSentinel = "__create__";

    /// <summary>
    /// Choice option key meaning "enter a custom Client ID (GUID)" via a follow-up text prompt.
    /// </summary>
    public const string CustomSentinel = "__custom__";

    public const string CustomClientIdInputName = "custom-client-id";

    public static bool IsCreateSentinel(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value, CreateSentinel, StringComparison.Ordinal);

    public static bool IsCustomSentinel(string? value) =>
        string.Equals(value, CustomSentinel, StringComparison.Ordinal);

    internal static string FormatLabel(string appName, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"Entra app — {appName}"
            : $"Entra app — {appName} ({displayName})";

    internal static string FormatCreateLabel(string? displayNameForCreate) =>
        string.IsNullOrWhiteSpace(displayNameForCreate)
            ? "Create new application"
            : $"Create new application ({displayNameForCreate})";

    internal static string FormatCustomLabel() => "Other (enter Client ID GUID)";

    internal static string FormatDescription(
        bool hasApps,
        string providerResourceName,
        string appName,
        string parameterName) =>
        hasApps
            ? $"Select an existing app registration for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'). Create for a new one, or Other for a custom Client ID (GUID)."
            : $"Select Create to provision a new app registration for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'), or Other for an existing Client ID (GUID). App list was empty or Graph enumeration failed (needs Application.Read.All).";

    internal static List<KeyValuePair<string, string>> BuildOptions(
        string? displayNameForCreate,
        IReadOnlyList<KeyValuePair<string, string>> apps)
    {
        var options = new List<KeyValuePair<string, string>>(2 + apps.Count)
        {
            new(CreateSentinel, FormatCreateLabel(displayNameForCreate))
        };
        options.AddRange(apps);
        options.Add(new(CustomSentinel, FormatCustomLabel()));
        return options;
    }

    /// <summary>
    /// Maps a Choice result (option key or display label) to create/custom sentinel or a raw ClientId.
    /// Returns <see langword="null"/> when the value is empty or unusable.
    /// </summary>
    internal static string? NormalizeChoiceValue(string? raw, string? displayNameForCreate)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (string.Equals(trimmed, CreateSentinel, StringComparison.Ordinal)
            || string.Equals(trimmed, FormatCreateLabel(displayNameForCreate), StringComparison.Ordinal)
            || string.Equals(trimmed, "Create new application", StringComparison.Ordinal)
            || trimmed.StartsWith("Create new application (", StringComparison.Ordinal))
        {
            return CreateSentinel;
        }

        if (string.Equals(trimmed, CustomSentinel, StringComparison.Ordinal)
            || string.Equals(trimmed, FormatCustomLabel(), StringComparison.Ordinal))
        {
            return CustomSentinel;
        }

        return trimmed;
    }

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ParameterResource clientIdParameter,
        ParameterResource tenantIdParameter,
        string appName,
        string? displayNameForCreate,
        string providerResourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clientIdParameter);
        ArgumentNullException.ThrowIfNull(tenantIdParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);

        var tenantId = await TryGetTenantIdAsync(tenantIdParameter, cancellationToken).ConfigureAwait(false);
        var apps = string.IsNullOrWhiteSpace(tenantId)
            ? []
            : await EntraAppRegistrationEnumerator.TryGetAppRegistrationOptionsAsync(
                    tenantId!,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        // Last InputGeneratorAnnotation wins in ParameterResource.CreateInput.
        // AllowCustomChoice=false → Aspire FluentSelect (reliable Create selection; defaults to first option).
        clientIdParameter.Annotations.Add(new InputGeneratorAnnotation(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = InputType.Choice,
            Label = FormatLabel(appName, displayNameForCreate),
            Description = FormatDescription(
                apps.Count > 0,
                providerResourceName,
                appName,
                parameter.Name),
            Required = true,
            AllowCustomChoice = false,
            Options = BuildOptions(displayNameForCreate, apps)
        }));

        await AuthParameterPrompt.EnsureReadyAsync(services, [clientIdParameter], cancellationToken)
            .ConfigureAwait(false);

        var raw = await clientIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeChoiceValue(raw, displayNameForCreate);
        if (normalized is null)
        {
            throw new OperationCanceledException(
                $"Parameter '{clientIdParameter.Name}' was not set.",
                cancellationToken);
        }

        if (IsCustomSentinel(normalized))
        {
            var customClientId = await PromptCustomClientIdAsync(services, cancellationToken)
                .ConfigureAwait(false);
            await AuthParameterValue.SetAsync(
                    services,
                    clientIdParameter,
                    customClientId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (string.Equals(normalized, CreateSentinel, StringComparison.Ordinal))
        {
            await AuthParameterValue.SetAsync(
                    services,
                    clientIdParameter,
                    CreateSentinel,
                    cancellationToken,
                    persistToDeploymentState: false)
                .ConfigureAwait(false);
            return;
        }

        if (!string.Equals(raw?.Trim(), normalized, StringComparison.Ordinal))
        {
            await AuthParameterValue.SetAsync(
                    services,
                    clientIdParameter,
                    normalized,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal static async Task<string> PromptCustomClientIdAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var interaction = services.GetService<IInteractionService>();
        if (interaction is null || !interaction.IsAvailable)
        {
            throw new InvalidOperationException(
                "Interaction service is unavailable. Set Parameters__* for the ClientId in CI.");
        }

        var input = new InteractionInput
        {
            Name = CustomClientIdInputName,
            InputType = InputType.Text,
            Label = "Client ID (GUID)",
            Description = "Enter an existing Entra application (client) ID.",
            Required = true
        };

        var result = await interaction.PromptInputsAsync(
                title: "Custom Client ID",
                message: "Paste the Entra application (client) ID GUID.",
                [input],
                options: null,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Canceled || result.Data is null)
        {
            throw new OperationCanceledException("Custom Client ID prompt was canceled.", cancellationToken);
        }

        var customClientId = result.Data.GetString(CustomClientIdInputName)?.Trim();
        if (string.IsNullOrWhiteSpace(customClientId) || !Guid.TryParse(customClientId, out _))
        {
            throw new InvalidOperationException("A valid Client ID GUID is required.");
        }

        return customClientId;
    }

    private static async Task<string?> TryGetTenantIdAsync(
        ParameterResource tenantIdParameter,
        CancellationToken cancellationToken)
    {
        try
        {
            return await tenantIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
