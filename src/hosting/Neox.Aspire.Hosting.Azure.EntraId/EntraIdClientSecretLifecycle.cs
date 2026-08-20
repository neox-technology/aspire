using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Run-mode helper: after the app registration is ready, create missing client secrets via Graph
/// <c>addPassword</c>, complete the Aspire parameter, and persist to user secrets.
/// </summary>
internal static class EntraIdClientSecretLifecycle
{
    private static readonly PropertyInfo? WaitForValueTcsProperty =
        typeof(ParameterResource).GetProperty(
            "WaitForValueTcs",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly PropertyInfo? ConfigurationKeyProperty =
        typeof(ParameterResource).GetProperty(
            "ConfigurationKey",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

#pragma warning disable ASPIREUSERSECRETS001
    internal static async Task OnAppRegistrationReadyAsync(
        AzureEntraIdAppRegistrationResource app,
        ResourceReadyEvent evt,
        CancellationToken cancellationToken)
    {
        if (app.ClientSecrets.Count == 0)
        {
            return;
        }

        var executionContext = evt.Services.GetService<DistributedApplicationExecutionContext>();
        if (executionContext is null || !executionContext.IsRunMode)
        {
            return;
        }

        var logger = evt.Services.GetService<ILoggerFactory>()?.CreateLogger("Neox.Aspire.Hosting.Azure.EntraId.WithSecret");
        var configuration = evt.Services.GetService<IConfiguration>();
        var userSecrets = evt.Services.GetService<IUserSecretsManager>()
            ?? evt.Services.GetService<IDistributedApplicationBuilder>()?.UserSecretsManager;
        var passwordClient = ResolvePasswordClient(app, evt.Services);

        string? objectId = null;

        foreach (var parameter in app.ClientSecrets)
        {
            var configKey = GetConfigurationKey(parameter);
            var existing = configuration?[configKey];
            if (string.IsNullOrEmpty(existing))
            {
                existing = TryGetCurrentParameterValue(parameter);
            }

            if (!string.IsNullOrEmpty(existing))
            {
                logger?.LogInformation(
                    "Client secret parameter {ParameterName} already has a value; skipping Graph addPassword.",
                    parameter.Name);
                EnsureParameterValue(parameter, existing);
                continue;
            }

            objectId ??= await app.ObjectId.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(objectId))
            {
                throw new InvalidOperationException(
                    $"App registration '{app.Name}' ObjectId output is empty; cannot create client secret '{parameter.Name}'.");
            }

            logger?.LogInformation(
                "Creating Entra client secret for parameter {ParameterName} on application {ObjectId}.",
                parameter.Name,
                objectId);

            var secretText = await passwordClient
                .AddPasswordAsync(objectId, parameter.Name, cancellationToken)
                .ConfigureAwait(false);

            EnsureParameterValue(parameter, secretText);

            if (userSecrets is { IsAvailable: true })
            {
                if (!userSecrets.TrySetSecret(configKey, secretText))
                {
                    logger?.LogWarning(
                        "Failed to persist client secret parameter {ParameterName} to user secrets ({ConfigKey}).",
                        parameter.Name,
                        configKey);
                }
                else
                {
                    logger?.LogInformation(
                        "Persisted client secret parameter {ParameterName} to user secrets ({ConfigKey}).",
                        parameter.Name,
                        configKey);
                }
            }
            else
            {
                logger?.LogWarning(
                    "User secrets are unavailable; client secret parameter {ParameterName} was set in-memory only.",
                    parameter.Name);
            }
        }
    }
#pragma warning restore ASPIREUSERSECRETS001

    internal static void EnsureDeferredEmptyDefault(ParameterResource parameter)
    {
        // Avoid ParameterProcessor interactive prompt before OnResourceReady fills the value.
        if (parameter.Default is null)
        {
            parameter.Default = new DeferredEmptyParameterDefault();
        }
    }

    private static IEntraIdAppPasswordClient ResolvePasswordClient(
        AzureEntraIdAppRegistrationResource app,
        IServiceProvider services)
    {
        if (app.TryGetLastAnnotation<EntraIdAppPasswordClientAnnotation>(out var annotation))
        {
            return annotation.Client;
        }

        return services.GetService<IEntraIdAppPasswordClient>() ?? new EntraIdAppPasswordClient();
    }

    private static string GetConfigurationKey(ParameterResource parameter)
    {
        if (ConfigurationKeyProperty?.GetValue(parameter) is string key && !string.IsNullOrEmpty(key))
        {
            return key;
        }

        return parameter.IsConnectionString ? $"ConnectionStrings:{parameter.Name}" : $"Parameters:{parameter.Name}";
    }

    private static string? TryGetCurrentParameterValue(ParameterResource parameter)
    {
        try
        {
            var tcs = WaitForValueTcsProperty?.GetValue(parameter);
            if (tcs is not null)
            {
                var taskProp = tcs.GetType().GetProperty("Task");
                if (taskProp?.GetValue(tcs) is Task<string> { IsCompletedSuccessfully: true } completed)
                {
                    return completed.Result;
                }
            }
        }
        catch
        {
            // Fall through.
        }

        return null;
    }

    private static void EnsureParameterValue(ParameterResource parameter, string value)
    {
        var tcsObj = WaitForValueTcsProperty?.GetValue(parameter);
        if (tcsObj is TaskCompletionSource<string> tcs)
        {
            if (tcs.Task.IsCompleted)
            {
                var replacement = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                WaitForValueTcsProperty!.SetValue(parameter, replacement);
                replacement.TrySetResult(value);
            }
            else
            {
                tcs.TrySetResult(value);
            }

            return;
        }

        var created = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        WaitForValueTcsProperty?.SetValue(parameter, created);
        created.TrySetResult(value);
    }

    /// <summary>
    /// Supplies an empty default so ParameterProcessor does not treat the secret as missing
    /// before <see cref="OnAppRegistrationReadyAsync"/> runs.
    /// </summary>
    private sealed class DeferredEmptyParameterDefault : ParameterDefault
    {
        public override string GetDefaultValue() => string.Empty;

        public override void WriteToManifest(global::Aspire.Hosting.Publishing.ManifestPublishingContext context)
        {
        }
    }
}

internal sealed class EntraIdAppPasswordClientAnnotation(IEntraIdAppPasswordClient client) : IResourceAnnotation
{
    public IEntraIdAppPasswordClient Client { get; } = client;
}

internal sealed class EntraIdClientSecretReadyAnnotation : IResourceAnnotation;
