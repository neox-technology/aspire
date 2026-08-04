using System.Reflection;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Sets Aspire parameter values produced by AuthOps (workload ClientId / secret / tenant)
/// and persists them using the same deployment-state contract as <c>ParameterProcessor</c>.
/// </summary>
internal static class AuthParameterValue
{
    /// <summary>
    /// Aspire stores each parameter under section <c>Parameters:{parameterName}</c> via <see cref="DeploymentStateSection.SetValue"/>.
    /// </summary>
    internal static string GetParametersSectionName(string parameterName) => $"Parameters:{parameterName}";

    public static async Task SetAsync(
        IServiceProvider services,
        ParameterResource parameter,
        string value,
        CancellationToken cancellationToken,
        bool persistToDeploymentState = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        ForceSetWaitForValueTcs(parameter, value);
        if (!persistToDeploymentState)
        {
            return;
        }

        await SaveDeploymentStateAsync(services, parameter.Name, value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets (or replaces) the resolved parameter value, including when a create sentinel was chosen earlier.
    /// Mirrors Aspire <c>ParameterProcessor</c> recreate-TCS behavior when the value is already completed.
    /// </summary>
    private static void ForceSetWaitForValueTcs(ParameterResource parameter, string value)
    {
        try
        {
            var prop = typeof(ParameterResource).GetProperty(
                "WaitForValueTcs",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (prop is null)
            {
                return;
            }

            var tcsObj = prop.GetValue(parameter);
            var completed = false;
            if (tcsObj is not null)
            {
                var taskProp = tcsObj.GetType().GetProperty("Task");
                if (taskProp?.GetValue(tcsObj) is Task { IsCompleted: true })
                {
                    completed = true;
                }
            }

            if (tcsObj is null || completed)
            {
                var tcsType = typeof(TaskCompletionSource<string>);
                var newTcs = Activator.CreateInstance(
                    tcsType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.CreateInstance,
                    binder: null,
                    args: [TaskCreationOptions.RunContinuationsAsynchronously],
                    culture: null);
                prop.SetValue(parameter, newTcs);
                tcsObj = newTcs;
            }

            if (tcsObj is null)
            {
                return;
            }

            var trySetResult = tcsObj.GetType().GetMethod("TrySetResult", [typeof(string)]);
            if (trySetResult is not null)
            {
                trySetResult.Invoke(tcsObj, [value]);
                return;
            }

            var setResult = tcsObj.GetType().GetMethod("SetResult", [typeof(string)]);
            setResult?.Invoke(tcsObj, [value]);
        }
        catch
        {
            // Best-effort; deployment state still persisted by caller.
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

        // Match Aspire ParameterProcessor: section name is ConfigurationKey ("Parameters:{name}"),
        // value stored via SetValue (root key ""), not a custom "Auth" bag keyed by name.
        var section = await stateManager
            .AcquireSectionAsync(GetParametersSectionName(parameterName), cancellationToken)
            .ConfigureAwait(false);
        section.SetValue(value);
        await stateManager.SaveSectionAsync(section, cancellationToken).ConfigureAwait(false);
    }
#pragma warning restore ASPIREPIPELINES002
}
