using System.Reflection;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Sets Aspire parameter values produced by AuthOps provision (workload ClientId / secret / tenant).
/// </summary>
internal static class AuthParameterValue
{
    public static async Task SetAsync(
        IServiceProvider services,
        ParameterResource parameter,
        string value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (TryCompleteWaitForValueTcs(parameter, value))
        {
            await SaveDeploymentStateAsync(services, parameter.Name, value, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Parameter already has a value (e.g. ExistingClientId default) — still persist state.
        await SaveDeploymentStateAsync(services, parameter.Name, value, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryCompleteWaitForValueTcs(ParameterResource parameter, string value)
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
            if (taskProp?.GetValue(tcsObj) is Task { IsCompleted: true })
            {
                return false;
            }

            var trySetResult = tcsObj.GetType().GetMethod("TrySetResult", [typeof(string)]);
            if (trySetResult is not null)
            {
                return trySetResult.Invoke(tcsObj, [value]) is true;
            }

            var setResult = tcsObj.GetType().GetMethod("SetResult", [typeof(string)]);
            setResult?.Invoke(tcsObj, [value]);
            return setResult is not null;
        }
        catch
        {
            return false;
        }
    }

#pragma warning disable ASPIREPIPELINES002
    private static async Task SaveDeploymentStateAsync(
        IServiceProvider services,
        string parameterName,
        string value,
        CancellationToken cancellationToken)
    {
        var stateManager = services.GetService<IDeploymentStateManager>();
        if (stateManager is null)
        {
            return;
        }

        var section = await stateManager.AcquireSectionAsync("Auth", cancellationToken).ConfigureAwait(false);
        section.Data[parameterName] = value;
        await stateManager.SaveSectionAsync(section, cancellationToken).ConfigureAwait(false);
    }
#pragma warning restore ASPIREPIPELINES002
}
