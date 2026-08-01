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
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ParameterResource tenantParameter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tenantParameter);

        // Prefetch before ParameterProcessor builds the interaction input.
        var tenants = await EntraTenantEnumerator.TryGetTenantOptionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Last InputGeneratorAnnotation wins in ParameterResource.CreateInput.
        tenantParameter.Annotations.Add(new InputGeneratorAnnotation(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = tenants.Count > 0 ? InputType.Choice : InputType.Text,
            Label = "Entra tenant",
            Description = tenants.Count > 0
                ? "Select an Entra tenant you can access (arrow keys), or Other for a custom GUID."
                : "Enter the Entra tenant id (GUID). Tenant enumeration was empty or failed.",
            Required = true,
            AllowCustomChoice = tenants.Count > 0,
            Options = tenants.Count > 0 ? tenants : null
        }));

        await AuthParameterPrompt.EnsureReadyAsync(services, [tenantParameter], cancellationToken)
            .ConfigureAwait(false);
    }
}
