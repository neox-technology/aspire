#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Prompts for an Auth app ClientId: select an existing IAM oauth client or paste a custom id.
/// Create is out of scope for Google AuthOps.
/// </summary>
internal static class GoogleOauthClientParameterPrompt
{
    /// <summary>
    /// Legacy Entra-style create sentinel — rejected by plan/provision with a clear error.
    /// </summary>
    public const string CreateSentinel = "__create__";

    public static bool IsCreateSentinel(string? value) =>
        string.Equals(value, CreateSentinel, StringComparison.Ordinal);

    internal static string FormatLabel(string appName, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"Google oauth client — {appName}"
            : $"Google oauth client — {appName} ({displayName})";

    internal static string FormatDescription(
        bool hasClients,
        string providerResourceName,
        string appName,
        string parameterName) =>
        hasClients
            ? $"Select an existing oauth client for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'), or Other to paste a Client ID. Google AuthOps does not create clients."
            : $"Paste an existing Client ID for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'). Client list was empty or IAM enumeration failed. Google AuthOps does not create clients.";

    internal static string FormatDescriptionPending(
        string providerResourceName,
        string appName,
        string parameterName) =>
        $"Select or paste an existing Client ID for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'). Existing clients load after a project is selected. Google AuthOps does not create clients.";

    public static void ConfigureClientIdChoiceInput(
        IResourceBuilder<ParameterResource> clientIdParam,
        string appName,
        string? displayNameForCreate,
        string providerResourceName,
        ParameterResource projectIdParameter)
    {
        ArgumentNullException.ThrowIfNull(clientIdParam);
        ArgumentNullException.ThrowIfNull(projectIdParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);

        if (clientIdParam.Resource.Annotations.OfType<InputGeneratorAnnotation>().Any())
        {
            return;
        }

        var projectParameterName = projectIdParameter.Name;

        clientIdParam.WithCustomInput(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = InputType.Choice,
            Label = FormatLabel(appName, displayNameForCreate),
            Description = FormatDescriptionPending(
                providerResourceName,
                appName,
                parameter.Name),
            Required = true,
            AllowCustomChoice = true,
            Options = [],
            DynamicLoading = new InputLoadOptions
            {
                DependsOnInputs = [projectParameterName],
                AlwaysLoadOnStart = true,
                LoadCallback = async context =>
                {
                    var projectId = await ResolveProjectIdAsync(
                            context,
                            projectParameterName,
                            projectIdParameter)
                        .ConfigureAwait(false);
                    var clients = string.IsNullOrWhiteSpace(projectId)
                        ? []
                        : await GoogleOauthClientEnumerator.TryGetOauthClientOptionsAsync(
                                projectId!,
                                cancellationToken: context.CancellationToken)
                            .ConfigureAwait(false);

                    context.Input.Options = clients.Count > 0 ? clients : [];
                }
            }
        });
    }

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ParameterResource clientIdParameter,
        ParameterResource projectIdParameter,
        string appName,
        string? displayNameForCreate,
        string providerResourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clientIdParameter);
        ArgumentNullException.ThrowIfNull(projectIdParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);

        var projectId = await TryGetProjectIdAsync(projectIdParameter, cancellationToken).ConfigureAwait(false);
        var clients = string.IsNullOrWhiteSpace(projectId)
            ? []
            : await GoogleOauthClientEnumerator.TryGetOauthClientOptionsAsync(
                    projectId!,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        clientIdParameter.Annotations.Add(new InputGeneratorAnnotation(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = clients.Count > 0 ? InputType.Choice : InputType.Text,
            Label = FormatLabel(appName, displayNameForCreate),
            Description = FormatDescription(
                clients.Count > 0,
                providerResourceName,
                appName,
                parameter.Name),
            Required = true,
            AllowCustomChoice = clients.Count > 0,
            Options = clients.Count > 0 ? clients : null
        }));

        await AuthParameterPrompt.EnsureReadyAsync(services, [clientIdParameter], cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string?> ResolveProjectIdAsync(
        LoadInputContext context,
        string projectParameterName,
        ParameterResource projectIdParameter)
    {
        if (context.AllInputs.TryGetByName(projectParameterName, out var projectInput)
            && !string.IsNullOrWhiteSpace(projectInput.Value))
        {
            return projectInput.Value;
        }

        return await TryGetProjectIdAsync(projectIdParameter, context.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> TryGetProjectIdAsync(
        ParameterResource projectIdParameter,
        CancellationToken cancellationToken)
    {
        try
        {
            return await projectIdParameter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
