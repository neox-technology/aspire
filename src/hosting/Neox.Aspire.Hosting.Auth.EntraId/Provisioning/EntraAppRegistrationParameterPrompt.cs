#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

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

    public static bool IsCreateSentinel(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || string.Equals(value, CreateSentinel, StringComparison.Ordinal);

    internal static string FormatLabel(string appName, string? displayName) =>
        string.IsNullOrWhiteSpace(displayName)
            ? $"Entra app — {appName}"
            : $"Entra app — {appName} ({displayName})";

    internal static string FormatDescription(
        bool hasApps,
        string providerResourceName,
        string appName,
        string parameterName) =>
        hasApps
            ? $"Select an existing app registration for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'). Create for a new one, or Other for a custom Client ID (GUID)."
            : $"Select Create to provision a new app registration for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'), or Other for an existing Client ID (GUID). App list was empty or Graph enumeration failed (needs Application.Read.All).";

    internal static string FormatDescriptionPending(
        string providerResourceName,
        string appName,
        string parameterName) =>
        $"Select Create to provision a new app registration for '{appName}' on provider '{providerResourceName}' (parameter '{parameterName}'), or Other for a custom Client ID (GUID). Existing apps load after a tenant is selected.";

    public static void ConfigureClientIdChoiceInput(
        IResourceBuilder<ParameterResource> clientIdParam,
        string appName,
        string? displayNameForCreate,
        string providerResourceName,
        ParameterResource tenantIdParameter)
    {
        ArgumentNullException.ThrowIfNull(clientIdParam);
        ArgumentNullException.ThrowIfNull(tenantIdParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);

        if (clientIdParam.Resource.Annotations.OfType<InputGeneratorAnnotation>().Any())
        {
            return;
        }

        var tenantParameterName = tenantIdParameter.Name;

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
            Options = BuildOptions(displayNameForCreate, apps: []),
            DynamicLoading = new InputLoadOptions
            {
                DependsOnInputs = [tenantParameterName],
                AlwaysLoadOnStart = true,
                LoadCallback = async context =>
                {
                    var tenantId = await ResolveTenantIdAsync(
                            context,
                            tenantParameterName,
                            tenantIdParameter)
                        .ConfigureAwait(false);
                    var apps = string.IsNullOrWhiteSpace(tenantId)
                        ? []
                        : await EntraAppRegistrationEnumerator.TryGetAppRegistrationOptionsAsync(
                                tenantId!,
                                cancellationToken: context.CancellationToken)
                            .ConfigureAwait(false);

                    context.Input.Options = BuildOptions(displayNameForCreate, apps);
                }
            }
        });
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
            AllowCustomChoice = true,
            Options = BuildOptions(displayNameForCreate, apps)
        }));

        await AuthParameterPrompt.EnsureReadyAsync(services, [clientIdParameter], cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<KeyValuePair<string, string>> BuildOptions(
        string? displayNameForCreate,
        IReadOnlyList<KeyValuePair<string, string>> apps)
    {
        var createLabel = string.IsNullOrWhiteSpace(displayNameForCreate)
            ? "Create new application"
            : $"Create new application ({displayNameForCreate})";

        var options = new List<KeyValuePair<string, string>>(1 + apps.Count)
        {
            new(CreateSentinel, createLabel)
        };
        options.AddRange(apps);
        return options;
    }

    private static async Task<string?> ResolveTenantIdAsync(
        LoadInputContext context,
        string tenantParameterName,
        ParameterResource tenantIdParameter)
    {
        if (context.AllInputs.TryGetByName(tenantParameterName, out var tenantInput)
            && !string.IsNullOrWhiteSpace(tenantInput.Value))
        {
            return tenantInput.Value;
        }

        return await TryGetTenantIdAsync(tenantIdParameter, context.CancellationToken).ConfigureAwait(false);
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
