using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Run-mode helper for <see cref="EntraIdPasswordCredentialResource"/>: Graph
/// <c>addPassword</c>, complete the Aspire parameter, persist to user secrets.
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
    internal static async Task EnsureSecretAsync(
        EntraIdPasswordCredentialResource credential,
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var executionContext = services.GetService<DistributedApplicationExecutionContext>();
        if (executionContext is null || !executionContext.IsRunMode)
        {
            return;
        }

        var parameter = credential.SecretParameter;
        var parent = credential.Parent;
        var configuration = services.GetService<IConfiguration>();
        var userSecrets = services.GetService<IUserSecretsManager>()
            ?? services.GetService<IDistributedApplicationBuilder>()?.UserSecretsManager;
        var passwordClient = ResolvePasswordClient(credential, services);

        var configKey = GetConfigurationKey(parameter);
        var existing = configuration?[configKey];
        if (string.IsNullOrEmpty(existing))
        {
            existing = TryGetCurrentParameterValue(parameter);
        }

        if (!string.IsNullOrEmpty(existing))
        {
            logger.LogInformation(
                "Client secret parameter {ParameterName} already has a value; skipping Graph addPassword.",
                parameter.Name);
            EnsureParameterValue(parameter, existing);
            return;
        }

        var notifications = services.GetService<ResourceNotificationService>();
        if (notifications is not null)
        {
            await notifications
                .WaitForResourceAsync(parent.Name, KnownResourceStates.Running, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            logger.LogDebug(
                "ResourceNotificationService is unavailable; proceeding without waiting for '{ParentName}'.",
                parent.Name);
        }

        var objectId = await parent.ObjectId.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(objectId))
        {
            throw new InvalidOperationException(
                $"App registration '{parent.Name}' ObjectId output is empty; cannot create client secret '{parameter.Name}'.");
        }

        logger.LogInformation(
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
                logger.LogWarning(
                    "Failed to persist client secret parameter {ParameterName} to user secrets ({ConfigKey}).",
                    parameter.Name,
                    configKey);
            }
            else
            {
                logger.LogInformation(
                    "Persisted client secret parameter {ParameterName} to user secrets ({ConfigKey}).",
                    parameter.Name,
                    configKey);
            }
        }
        else
        {
            logger.LogWarning(
                "User secrets are unavailable; client secret parameter {ParameterName} was set in-memory only.",
                parameter.Name);
        }
    }
#pragma warning restore ASPIREUSERSECRETS001

    internal static void EnsureDeferredEmptyDefault(ParameterResource parameter)
    {
        if (parameter.Default is null)
        {
            parameter.Default = new DeferredEmptyParameterDefault();
        }
    }

    internal static void BindClientSecretEnvironment<TResource>(
        IResourceBuilder<TResource> builder,
        AzureEntraIdAppRegistrationResource parent,
        string sectionName)
        where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        if (parent.PasswordCredentials.Count == 0)
        {
            return;
        }

        builder.WithEnvironment($"{sectionName}ClientSecret", parent.PasswordCredentials[0].SecretParameter);

        for (var i = 1; i < parent.PasswordCredentials.Count; i++)
        {
            var index = parent.KeyCredentials.Count + (i - 1);
            var secret = parent.PasswordCredentials[i].SecretParameter;
            builder.WithEnvironment($"{sectionName}ClientCredentials__{index}__SourceType", "ClientSecret");
            builder.WithEnvironment($"{sectionName}ClientCredentials__{index}__ClientSecret", secret);
        }

        foreach (var credential in parent.PasswordCredentials)
        {
            if (!builder.Resource.Annotations.OfType<WaitAnnotation>().Any(wait => ReferenceEquals(wait.Resource, credential)))
            {
                builder.WaitFor(builder.ApplicationBuilder.CreateResourceBuilder(credential));
            }
        }
    }

    private static IEntraIdAppPasswordClient ResolvePasswordClient(
        EntraIdPasswordCredentialResource credential,
        IServiceProvider services)
    {
        if (credential.TryGetLastAnnotation<EntraIdAppPasswordClientAnnotation>(out var onCredential))
        {
            return onCredential.Client;
        }

        if (credential.Parent.TryGetLastAnnotation<EntraIdAppPasswordClientAnnotation>(out var onParent))
        {
            return onParent.Client;
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
