#pragma warning disable ASPIREPIPELINES002

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class WithClientSecretTests
{
    [Fact]
    public void WithClientSecret_WithoutParam_KeepsAutoParameter_AndAnnotates()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var api = entra.AddAppRegistration("api", "Api").WithClientSecret();

        var annotation = Assert.Single(api.Resource.Annotations.OfType<ClientSecretAnnotation>());
        Assert.Equal("api-clientsecret", annotation.SecretResource.Name);
        Assert.Same(api.Resource, annotation.SecretResource.Owner);
        Assert.Equal("entra-api-client-secret", api.Resource.ClientSecretParameter.Name);
        Assert.True(api.Resource.ClientSecretParameter.Secret);
        Assert.Contains(
            annotation.SecretResource.Annotations.OfType<ResourceCommandAnnotation>(),
            c => c.Name == EntraAuthClientSecretCommandExtensions.CreateClientSecretCommandName);
        AssertHasParentRelationship(annotation.SecretResource, api.Resource);
        AssertHasParentRelationship(api.Resource.ClientSecretParameter, annotation.SecretResource);
    }

    [Fact]
    public void WithClientSecret_WithParam_OverridesClientSecretParameter()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var secret = builder.AddParameter("custom-secret", secret: true);
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var api = entra.AddAppRegistration("api", "Api").WithClientSecret(secret);

        Assert.Same(secret.Resource, api.Resource.ClientSecretParameter);
        var annotation = Assert.Single(api.Resource.Annotations.OfType<ClientSecretAnnotation>());
        Assert.Equal("api-clientsecret", annotation.SecretResource.Name);
        AssertHasParentRelationship(secret.Resource, annotation.SecretResource);
    }

    [Fact]
    public void WithClientSecret_Idempotent_DoesNotDuplicateResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var api = entra.AddAppRegistration("api", "Api")
            .WithClientSecret()
            .WithClientSecret();

        Assert.Single(api.Resource.Annotations.OfType<ClientSecretAnnotation>());
        Assert.Single(builder.Resources.OfType<EntraClientSecretResource>());
    }

    private static void AssertHasParentRelationship(IResource child, IResource parent)
    {
        Assert.Contains(
            child.Annotations.OfType<ResourceRelationshipAnnotation>(),
            a => a.Type == "Parent" && ReferenceEquals(a.Resource, parent));
    }

    [Fact]
    public void WithClientSecret_NonSecretParam_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var notSecret = builder.AddParameter("plain", "value");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var api = entra.AddAppRegistration("api", "Api");

        var ex = Assert.Throws<ArgumentException>(() => api.WithClientSecret(notSecret));
        Assert.Contains("secret: true", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithAuth_WithClientSecret_EmitsClientSecret()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddAppRegistration("spa", "Spa").WithClientSecret();

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa);

        // Instance + TenantId + ClientId + ClientSecret
        Assert.Equal(4, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public void WithAuth_WithClientSecret_IncludeClientSecretFalse_OmitsSecret()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var spa = entra.AddAppRegistration("spa", "Spa").WithClientSecret();

        var ops = builder.AddContainer("ops", "mcr.microsoft.com/dotnet/runtime", "10.0");
        ops.WithAuth(spa, env => env.IncludeClientSecret = false);

        // Instance + TenantId + ClientId
        Assert.Equal(3, ops.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>().Count());
    }

    [Fact]
    public async Task FakeProvisioner_AddPassword_ReturnsSecret_AndTracksCalls()
    {
        var fake = new FakeEntraGraphAppProvisioner { NextSecretText = "one-shot" };
        var end = DateTimeOffset.Parse("2027-08-02T00:00:00Z");
        var secret = await fake.AddPasswordCredentialAsync("obj-1", "neox-local", end, CancellationToken.None);

        Assert.Equal("one-shot", secret);
        Assert.Equal(1, fake.AddPasswordCallCount);
        Assert.Equal("obj-1", fake.LastPasswordObjectId);
        Assert.Equal("neox-local", fake.LastPasswordDisplayName);
        Assert.Equal(end, fake.LastPasswordEndDateTime);
    }

    [Fact]
    public void EntraClientSecretLifetime_ResolveEndDateTime_MapsPresets()
    {
        var now = DateTimeOffset.Parse("2026-08-02T12:00:00Z");

        Assert.Equal(now.AddMonths(6), EntraClientSecretLifetime.ResolveEndDateTime("6", now));
        Assert.Equal(now.AddMonths(12), EntraClientSecretLifetime.ResolveEndDateTime("12", now));
        Assert.Equal(now.AddMonths(24), EntraClientSecretLifetime.ResolveEndDateTime("24", now));
        Assert.Equal(now.AddMonths(12), EntraClientSecretLifetime.ResolveEndDateTime(null, now));
        Assert.Equal(now.AddMonths(12), EntraClientSecretLifetime.ResolveEndDateTime("bogus", now));
    }

    [Fact]
    public async Task FakeProvisioner_ProvisionAsync_DoesNotCallAddPassword()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tenant = builder.AddParameter("t", "t");
        var entra = builder.AddAuthProvider("entra").Entra(o => o.TenantId = tenant);
        var app = entra.AddAppRegistration("api", "Api").WithClientSecret().Resource;

        var fake = new FakeEntraGraphAppProvisioner();
        var plan = await fake.PlanAsync(app, CancellationToken.None);
        await fake.ProvisionAsync(app, plan, CancellationToken.None);

        Assert.Equal(0, fake.AddPasswordCallCount);
        Assert.Null((await fake.ProvisionAsync(app, plan, CancellationToken.None)).ClientSecret);
    }

    [Fact]
    public async Task AuthParameterValue_PersistsSecret_UnderParametersSection()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secretParam = builder.AddParameter("entra-api-client-secret", secret: true).Resource;

        var state = new FakeDeploymentStateManager();
        await using var services = new ServiceCollection()
            .AddSingleton<IDeploymentStateManager>(state)
            .BuildServiceProvider();

        await AuthParameterValue.SetAsync(
            services,
            secretParam,
            "persisted-secret",
            CancellationToken.None);

        Assert.Contains("Parameters:entra-api-client-secret", state.SavedSectionNames);
        Assert.Equal("persisted-secret", state.Sections["Parameters:entra-api-client-secret"].Data[""]!.GetValue<string>());
    }

    private sealed class FakeDeploymentStateManager : IDeploymentStateManager
    {
        public Dictionary<string, DeploymentStateSection> Sections { get; } = new(StringComparer.Ordinal);
        public List<string> SavedSectionNames { get; } = [];
        public string? StateFilePath => null;

        public Task<DeploymentStateSection> AcquireSectionAsync(
            string sectionName,
            CancellationToken cancellationToken = default)
        {
            if (!Sections.TryGetValue(sectionName, out var section))
            {
                section = new DeploymentStateSection(sectionName, new JsonObject(), version: 0);
                Sections[sectionName] = section;
            }

            return Task.FromResult(section);
        }

        public Task SaveSectionAsync(
            DeploymentStateSection section,
            CancellationToken cancellationToken = default)
        {
            Sections[section.SectionName] = section;
            SavedSectionNames.Add(section.SectionName);
            return Task.CompletedTask;
        }

        public Task DeleteSectionAsync(
            DeploymentStateSection section,
            CancellationToken cancellationToken = default)
        {
            Sections.Remove(section.SectionName);
            return Task.CompletedTask;
        }

        public Task ClearAllStateAsync(CancellationToken cancellationToken = default)
        {
            Sections.Clear();
            SavedSectionNames.Clear();
            return Task.CompletedTask;
        }
    }
}
