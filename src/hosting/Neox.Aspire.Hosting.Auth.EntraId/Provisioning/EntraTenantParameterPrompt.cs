#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Prompts for the Entra tenant using a Choice list (arrow-key selection in the CLI).
/// Prefetches ARM tenants so Options are non-empty — Aspire CLI falls back to raw text when Options is empty.
/// </summary>
internal static class EntraTenantParameterPrompt
{
    internal static string FormatLabel(string providerResourceName) =>
        $"Entra tenant — {providerResourceName}";

    internal static string FormatDescription(bool hasTenants, string providerResourceName, string parameterName) =>
        hasTenants
            ? $"Select an Entra tenant for Auth provider '{providerResourceName}' (parameter '{parameterName}'). Use arrow keys, or Other for a custom GUID."
            : $"Enter the Entra tenant id (GUID) for Auth provider '{providerResourceName}' (parameter '{parameterName}'). Tenant enumeration was empty or failed.";

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ParameterResource tenantParameter,
        string providerResourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tenantParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);

        // Prefetch before ParameterProcessor builds the interaction input.
        var tenants = await EntraTenantEnumerator.TryGetTenantOptionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Last InputGeneratorAnnotation wins in ParameterResource.CreateInput.
        tenantParameter.Annotations.Add(new InputGeneratorAnnotation(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = tenants.Count > 0 ? InputType.Choice : InputType.Text,
            Label = FormatLabel(providerResourceName),
            Description = FormatDescription(tenants.Count > 0, providerResourceName, parameter.Name),
            Required = true,
            AllowCustomChoice = tenants.Count > 0,
            Options = tenants.Count > 0 ? tenants : null
        }));

        await AuthParameterPrompt.EnsureReadyAsync(services, [tenantParameter], cancellationToken)
            .ConfigureAwait(false);
    }
}
