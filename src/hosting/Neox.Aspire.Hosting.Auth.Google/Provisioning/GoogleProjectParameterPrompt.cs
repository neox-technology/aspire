#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Prompts for the Google Cloud project id using a Choice list.
/// </summary>
internal static class GoogleProjectParameterPrompt
{
    internal static string FormatLabel(string providerResourceName) =>
        $"Google project — {providerResourceName}";

    internal static string FormatDescription(bool hasProjects, string providerResourceName, string parameterName) =>
        hasProjects
            ? $"Select a Google Cloud project for Auth provider '{providerResourceName}' (parameter '{parameterName}'). Use arrow keys, or Other for a custom project id."
            : $"Enter the Google Cloud project id for Auth provider '{providerResourceName}' (parameter '{parameterName}'). Project enumeration was empty or failed (needs ADC).";

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        ParameterResource projectParameter,
        string providerResourceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(projectParameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerResourceName);

        var projects = await GoogleProjectEnumerator.TryGetProjectOptionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        projectParameter.Annotations.Add(new InputGeneratorAnnotation(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = projects.Count > 0 ? InputType.Choice : InputType.Text,
            Label = FormatLabel(providerResourceName),
            Description = FormatDescription(projects.Count > 0, providerResourceName, parameter.Name),
            Required = true,
            AllowCustomChoice = projects.Count > 0,
            Options = projects.Count > 0 ? projects : null
        }));

        await AuthParameterPrompt.EnsureReadyAsync(services, [projectParameter], cancellationToken)
            .ConfigureAwait(false);
    }
}
