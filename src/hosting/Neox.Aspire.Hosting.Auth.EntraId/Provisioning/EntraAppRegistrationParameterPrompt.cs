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

    public static void ConfigureClientIdChoiceInput(
        IResourceBuilder<ParameterResource> clientIdParam,
        string? displayNameForCreate)
    {
        ArgumentNullException.ThrowIfNull(clientIdParam);

        if (clientIdParam.Resource.Annotations.OfType<InputGeneratorAnnotation>().Any())
        {
            return;
        }

        clientIdParam.WithCustomInput(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = InputType.Choice,
            Label = "Entra application (Client ID)",
            Description =
                "Select Create to provision a new app registration, pick an existing app, or enter a Client ID (GUID).",
            Required = true,
            AllowCustomChoice = true,
            Options = BuildOptions(displayNameForCreate, apps: [])
        });
    }

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ParameterResource clientIdParameter,
        ParameterResource tenantIdParameter,
        string? displayNameForCreate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(clientIdParameter);
        ArgumentNullException.ThrowIfNull(tenantIdParameter);

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
            Label = "Entra application (Client ID)",
            Description = apps.Count > 0
                ? "Select an existing app registration (arrow keys), Create for a new one, or Other for a custom Client ID (GUID)."
                : "Select Create to provision a new app registration, or Other for an existing Client ID (GUID). App list was empty or Graph enumeration failed (needs Application.Read.All).",
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
