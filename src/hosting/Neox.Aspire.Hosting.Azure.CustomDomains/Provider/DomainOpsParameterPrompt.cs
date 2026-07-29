using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Azure;

internal enum DomainOpsActionKind
{
    Verify,
    Guard,
    Provision
}

/// <summary>
/// Ensures unresolved Aspire parameters are prompted before <c>GetValueAsync</c> waits forever.
/// </summary>
internal static class DomainOpsParameterPrompt
{
    public static IEnumerable<ParameterResource> CollectRequired(
        DomainOpsActionKind kind,
        ParameterResource customDomain,
        ParameterResource certificateName,
        DomainOpsProviderResource provider,
        AzureCustomDomainOpsOptions options)
    {
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);

        switch (kind)
        {
            case DomainOpsActionKind.Verify:
                yield return customDomain;
                if (options.RequireCertificateName)
                {
                    yield return certificateName;
                }

                break;
            case DomainOpsActionKind.Guard:
                if (options.RequireCertificateName)
                {
                    yield return certificateName;
                }

                break;
            case DomainOpsActionKind.Provision:
                yield return customDomain;
                yield return certificateName;
                foreach (var auth in provider.AuthParameters.Values)
                {
                    yield return auth;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

#pragma warning disable ASPIREINTERACTION001
    /// <summary>
    /// Prompts for any parameters whose <c>WaitForValueTcs</c> is still incomplete.
    /// </summary>
    /// <exception cref="InvalidOperationException">Interaction/processor unavailable while parameters are unresolved.</exception>
    /// <exception cref="OperationCanceledException">User dismissed a parameter prompt.</exception>
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        IEnumerable<ParameterResource> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parameters);

        var pending = parameters
            .Where(IsWaitingForValue)
            .DistinctBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        var interaction = services.GetService<IInteractionService>();
        var processor = services.GetService<ParameterProcessor>();
        if (interaction is null || !interaction.IsAvailable || processor is null)
        {
            throw new InvalidOperationException(
                $"Unresolved parameters ({string.Join(", ", pending.Select(p => p.Name))}) and the interaction service is unavailable. Set Parameters__* or use each parameter's Set command in the dashboard.");
        }

        foreach (var parameter in pending)
        {
            await processor.SetParameterAsync(parameter, cancellationToken).ConfigureAwait(false);

            if (IsWaitingForValue(parameter))
            {
                throw new OperationCanceledException(
                    $"Parameter '{parameter.Name}' was not set.",
                    cancellationToken);
            }
        }
    }
#pragma warning restore ASPIREINTERACTION001

    public static bool IsWaitingForValue(ParameterResource parameter)
    {
        try
        {
            var prop = typeof(ParameterResource).GetProperty(
                "WaitForValueTcs",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var tcsObj = prop?.GetValue(parameter);
            if (tcsObj is null)
            {
                return false;
            }

            var taskProp = tcsObj.GetType().GetProperty("Task");
            return taskProp?.GetValue(tcsObj) is Task { IsCompleted: false };
        }
        catch
        {
            return false;
        }
    }
}
