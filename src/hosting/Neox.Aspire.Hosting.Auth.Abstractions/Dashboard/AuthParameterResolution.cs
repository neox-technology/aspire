using System.Reflection;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Non-blocking parameter value probes for AuthOps dashboard status (never hangs on unresolved params).
/// </summary>
internal static class AuthParameterResolution
{
    /// <summary>
    /// Attempts to read a resolved parameter value without waiting for interactive prompts.
    /// </summary>
    public static bool TryGetResolvedValue(
        ParameterResource parameter,
        IServiceProvider? services,
        out string? value)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        value = null;

        if (AuthParameterPrompt.DomainOpsStyleIsWaitingForValue(parameter))
        {
            return false;
        }

        if (TryReadCompletedWaitValue(parameter, out value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (services is not null)
        {
            var configuration = services.GetService<IConfiguration>();
            if (configuration is not null)
            {
                var fromConfig = FirstNonEmpty(
                    configuration[$"Parameters:{parameter.Name}"],
                    configuration[$"Parameters__{parameter.Name}"]);
                if (!string.IsNullOrWhiteSpace(fromConfig))
                {
                    value = fromConfig;
                    return true;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadCompletedWaitValue(ParameterResource parameter, out string? value)
    {
        value = null;
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
            if (taskProp?.GetValue(tcsObj) is not Task task || !task.IsCompletedSuccessfully)
            {
                return false;
            }

            // Task<string?> / Task<string>
            var resultProp = task.GetType().GetProperty("Result");
            if (resultProp?.GetValue(task) is string s)
            {
                value = s;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
