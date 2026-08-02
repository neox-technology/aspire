#pragma warning disable ASPIREPIPELINES002

using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthParameterValueTests
{
    [Fact]
    public void GetParametersSectionName_MatchesAspireConfigurationKey()
    {
        Assert.Equal("Parameters:provider-entra-tenant-id", AuthParameterValue.GetParametersSectionName("provider-entra-tenant-id"));
    }

    [Fact]
    public async Task SetAsync_WritesParametersSectionViaSetValue_NotAuthBag()
    {
        var builder = DistributedApplication.CreateBuilder();
        var parameter = builder.AddParameter("provider-entra-tenant-id", secret: false).Resource;

        var state = new FakeDeploymentStateManager();
        var services = new ServiceCollection()
            .AddSingleton<IDeploymentStateManager>(state)
            .BuildServiceProvider();

        await AuthParameterValue.SetAsync(
            services,
            parameter,
            "11111111-1111-1111-1111-111111111111",
            CancellationToken.None);

        Assert.DoesNotContain("Auth", state.SavedSectionNames);
        Assert.Contains("Parameters:provider-entra-tenant-id", state.SavedSectionNames);

        var section = state.Sections["Parameters:provider-entra-tenant-id"];
        Assert.Equal("11111111-1111-1111-1111-111111111111", section.Data[""]!.GetValue<string>());
        Assert.False(section.Data.ContainsKey("provider-entra-tenant-id"));
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
