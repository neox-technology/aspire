using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neox.Aspire.Hosting.Azure;
using Xunit;

#pragma warning disable ASPIREUSERSECRETS001

namespace Neox.Aspire.Hosting.Azure.EntraId.Tests;

public sealed class WithSecretTests
{
    [Fact]
    public void WithSecret_creates_password_credential_child_of_parent()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret);

        var credential = Assert.Single(app.Resource.PasswordCredentials);
        Assert.Same(secret.Resource, credential.SecretParameter);
        Assert.Same(app.Resource, credential.Parent);
        Assert.Equal("api-api-client-secret", credential.Name);
        Assert.NotNull(secret.Resource.Default);
        Assert.IsType<EntraIdPasswordCredentialResource>(credential);
        Assert.IsAssignableFrom<IResourceWithParent<AzureEntraIdAppRegistrationResource>>(credential);
        Assert.DoesNotContain(
            credential.Annotations.OfType<WaitAnnotation>(),
            wait => ReferenceEquals(wait.Resource, app.Resource));
    }

    [Fact]
    public void WithSecret_duplicate_parameter_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret);

        var ex = Assert.Throws<ArgumentException>(() => app.WithSecret(secret));
        Assert.Equal("secret", ex.ParamName);
    }

    [Fact]
    public void WithSecret_non_secret_parameter_throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var notSecret = builder.AddParameter("plain");
        var app = builder.AddAzureAppRegistration("api");

        var ex = Assert.Throws<ArgumentException>(() => app.WithSecret(notSecret));
        Assert.Equal("secret", ex.ParamName);
    }

    [Fact]
    public void WithSecret_accumulates_multiple_password_credentials()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret1 = builder.AddParameter("secret-one", secret: true);
        var secret2 = builder.AddParameter("secret-two", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret1)
            .WithSecret(secret2);

        Assert.Equal(2, app.Resource.PasswordCredentials.Count);
        Assert.All(
            app.Resource.PasswordCredentials,
            credential =>
            {
                Assert.Same(app.Resource, credential.Parent);
                Assert.IsAssignableFrom<IResourceWithParent<AzureEntraIdAppRegistrationResource>>(credential);
                Assert.DoesNotContain(
                    credential.Annotations.OfType<WaitAnnotation>(),
                    wait => ReferenceEquals(wait.Resource, app.Resource));
            });
    }

    [Fact]
    public void GetBicepTemplateString_with_secret_does_not_emit_password_credentials()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("passwordCredentials", bicep, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretText", bicep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetBicepTemplateString_existing_with_secret_does_not_emit_password_credentials()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret)
            .RunAsExisting("already-registered", resourceGroup: null!);

        var bicep = app.Resource.GetBicepTemplateString();
        Assert.DoesNotContain("passwordCredentials", bicep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureSecret_calls_addPassword_when_parameter_missing()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret);

        var credential = Assert.Single(app.Resource.PasswordCredentials);
        app.Resource.Outputs[AzureEntraIdAppRegistrationResource.ObjectIdOutputName] = "obj-123";

        var fakeGraph = new FakePasswordClient("generated-secret-text");
        credential.Annotations.Add(new EntraIdAppPasswordClientAnnotation(fakeGraph));

        var userSecrets = new FakeUserSecretsManager();
        var services = new ServiceCollection();
        services.AddSingleton(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IUserSecretsManager>(userSecrets);
        var sp = services.BuildServiceProvider();

        await EntraIdClientSecretLifecycle.EnsureSecretAsync(
            credential,
            sp,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Single(fakeGraph.Calls);
        Assert.Equal("obj-123", fakeGraph.Calls[0].ObjectId);
        Assert.Equal("api-client-secret", fakeGraph.Calls[0].DisplayName);
        Assert.True(userSecrets.TryGet("Parameters:api-client-secret", out var stored));
        Assert.Equal("generated-secret-text", stored);

        var value = await secret.Resource.GetValueAsync(CancellationToken.None);
        Assert.Equal("generated-secret-text", value);
    }

    [Fact]
    public async Task EnsureSecret_skips_graph_when_configuration_has_value()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret);

        var credential = Assert.Single(app.Resource.PasswordCredentials);
        app.Resource.Outputs[AzureEntraIdAppRegistrationResource.ObjectIdOutputName] = "obj-123";

        var fakeGraph = new FakePasswordClient("should-not-be-used");
        credential.Annotations.Add(new EntraIdAppPasswordClientAnnotation(fakeGraph));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Parameters:api-client-secret"] = "existing-from-user-secrets"
            })
            .Build();

        var userSecrets = new FakeUserSecretsManager();
        var services = new ServiceCollection();
        services.AddSingleton(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run));
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IUserSecretsManager>(userSecrets);
        var sp = services.BuildServiceProvider();

        await EntraIdClientSecretLifecycle.EnsureSecretAsync(
            credential,
            sp,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Empty(fakeGraph.Calls);
        Assert.Empty(userSecrets.SetCalls);

        var value = await secret.Resource.GetValueAsync(CancellationToken.None);
        Assert.Equal("existing-from-user-secrets", value);
    }

    [Fact]
    public async Task EnsureSecret_noop_outside_run_mode()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("api-client-secret", secret: true);
        var app = builder.AddAzureAppRegistration("api")
            .WithSecret(secret);

        var credential = Assert.Single(app.Resource.PasswordCredentials);
        app.Resource.Outputs[AzureEntraIdAppRegistrationResource.ObjectIdOutputName] = "obj-123";

        var fakeGraph = new FakePasswordClient("should-not-be-used");
        credential.Annotations.Add(new EntraIdAppPasswordClientAnnotation(fakeGraph));

        var services = new ServiceCollection();
        services.AddSingleton(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var sp = services.BuildServiceProvider();

        await EntraIdClientSecretLifecycle.EnsureSecretAsync(
            credential,
            sp,
            NullLogger.Instance,
            CancellationToken.None);

        Assert.Empty(fakeGraph.Calls);
    }

    private sealed class FakePasswordClient(string secretText) : IEntraIdAppPasswordClient
    {
        public List<(string ObjectId, string DisplayName)> Calls { get; } = [];

        public Task<string> AddPasswordAsync(string objectId, string displayName, CancellationToken cancellationToken)
        {
            Calls.Add((objectId, displayName));
            return Task.FromResult(secretText);
        }
    }

    private sealed class FakeUserSecretsManager : IUserSecretsManager
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public bool IsAvailable => true;

        public string FilePath => string.Empty;

        public List<(string Name, string Value)> SetCalls { get; } = [];

        public bool TrySetSecret(string name, string value)
        {
            SetCalls.Add((name, value));
            _secrets[name] = value;
            return true;
        }

        public bool TryGet(string name, out string? value) => _secrets.TryGetValue(name, out value);

        public bool TryDeleteSecret(string name) => _secrets.Remove(name);

        public void GetOrSetSecret(IConfigurationManager configuration, string name, Func<string> valueGenerator)
            => throw new NotSupportedException();

        public Task SaveStateAsync(System.Text.Json.Nodes.JsonObject state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
