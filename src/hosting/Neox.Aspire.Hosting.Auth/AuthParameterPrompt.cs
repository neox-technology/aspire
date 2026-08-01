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

        var pending = parameters
            .Where(DomainOpsStyleIsWaitingForValue)
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
                $"Unresolved parameters ({string.Join(", ", pending.Select(p => p.Name))}) and the interaction service is unavailable. " +
                "Set Parameters__* (e.g. Parameters__entra-tenant-id) or use each parameter's Set command in the dashboard.");
        }

        foreach (var parameter in pending)
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
