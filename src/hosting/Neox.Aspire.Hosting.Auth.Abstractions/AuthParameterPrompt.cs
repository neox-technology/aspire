using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Ensures unresolved Aspire parameters are prompted before <c>GetValueAsync</c> waits forever.
/// </summary>
internal static class AuthParameterPrompt
{
#pragma warning disable ASPIREINTERACTION001
    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        IEnumerable<ParameterResource> parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parameters);

        var all = parameters.DistinctBy(p => p.Name, StringComparer.Ordinal).ToList();
        if (all.Count == 0)
        {
            return;
        }

        var uninitialized = all.Where(p => InspectWaitState(p).TcsNull).ToList();
        var waiting = all.Where(DomainOpsStyleIsWaitingForValue).ToList();

        if (uninitialized.Count == 0 && waiting.Count == 0)
        {
            return;
        }

        var interaction = services.GetService<IInteractionService>();
        var processor = services.GetService<ParameterProcessor>();
        if (interaction is null || !interaction.IsAvailable || processor is null)
        {
            throw new InvalidOperationException(
                $"Unresolved parameters ({string.Join(", ", all.Select(p => p.Name))}) and the interaction service is unavailable. " +
                "Set Parameters__* (e.g. Parameters__auth-provider-entra-tenant-id) or use each parameter's Set command in the dashboard.");
        }

        // Pipeline / aspire do does not always run ParameterProcessor startup init.
        // Without WaitForValueTcs, treating only "waiting" params skips prompts entirely.
        if (uninitialized.Count > 0)
        {
            await processor.InitializeParametersAsync(uninitialized, waitForResolution: true)
                .ConfigureAwait(false);
        }

        waiting = all.Where(DomainOpsStyleIsWaitingForValue).ToList();
        foreach (var parameter in waiting)
        {
            await processor.SetParameterAsync(parameter, cancellationToken).ConfigureAwait(false);

            if (DomainOpsStyleIsWaitingForValue(parameter))
            {
                throw new OperationCanceledException(
                    $"Parameter '{parameter.Name}' was not set.",
                    cancellationToken);
            }
        }
    }
#pragma warning restore ASPIREINTERACTION001

    internal static bool DomainOpsStyleIsWaitingForValue(ParameterResource parameter)
    {
        var (_, waiting, _) = InspectWaitState(parameter);
        return waiting;
    }

    private static (bool TcsNull, bool Waiting, bool? Completed) InspectWaitState(ParameterResource parameter)
    {
        try
        {
            var prop = typeof(ParameterResource).GetProperty(
                "WaitForValueTcs",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var tcsObj = prop?.GetValue(parameter);
            if (tcsObj is null)
            {
                return (true, false, null);
            }

            var taskProp = tcsObj.GetType().GetProperty("Task");
            if (taskProp?.GetValue(tcsObj) is not Task task)
            {
                return (false, false, null);
            }

            return (false, !task.IsCompleted, task.IsCompleted);
        }
        catch
        {
            return (true, false, null);
        }
    }
}
