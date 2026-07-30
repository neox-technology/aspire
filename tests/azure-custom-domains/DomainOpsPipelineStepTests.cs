#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neox.Aspire.Hosting.Azure.Processes;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsPipelineStepTests
{
    [Fact]
    public void StepNameHelpers_MatchAspireConventions()
    {
        Assert.Equal("prereq-domain", AzureCustomDomainOpsExtensions.DomainPrereqStepName);
        Assert.Equal("prereq-domain-ovh", AzureCustomDomainOpsExtensions.GetDomainPrereqProviderStepName("ovh"));
        Assert.Equal("prereq-domain-cloudflare", AzureCustomDomainOpsExtensions.GetDomainPrereqProviderStepName("cloudflare"));
        Assert.Equal("provision-api-domain", AzureCustomDomainOpsExtensions.GetDomainProvisionStepName("api"));
        Assert.Equal("provision-api-containerapp", AzureCustomDomainOpsExtensions.GetContainerAppProvisionStepName("api"));
    }

    [Fact]
    public async Task RegistersPrereqAndProvisionSteps_WithExpectedDependsOn()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var customDomain = builder.AddParameter("customDomain", "example.com");
        var certificateName = builder.AddParameter("certificateName");
        var dns = builder.AddDomainOpsProvider("dns").Ovh();

        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps(customDomain, certificateName, dns);

        var steps = await CollectDomainOpsStepsAsync(builder);

        var prereqDomain = Assert.Single(steps, s => s.Name == "prereq-domain");
        Assert.Contains("provision-aca-env", prereqDomain.DependsOnSteps);

        var prereqOvh = Assert.Single(steps, s => s.Name == "prereq-domain-ovh");
        Assert.Contains("prereq-domain", prereqOvh.DependsOnSteps);

        var provisionDomain = Assert.Single(steps, s => s.Name == "provision-api-domain");
        Assert.Contains("prereq-domain-ovh", provisionDomain.DependsOnSteps);
        // provision-api-containerapp is wired later via PipelineConfigurationAnnotation once the
        // out-of-model AzureContainerAppResource exists (after prepare-azure-container-apps).
        Assert.DoesNotContain("provision-api-containerapp", provisionDomain.DependsOnSteps);

        Assert.Contains(steps, s => s.Name == "domain-verify");
        Assert.Contains(steps, s => s.Name == "domain-guard");
        Assert.DoesNotContain(steps, s => s.Name == "domain-provision");

        var domainOps = Assert.Single(builder.Resources.OfType<AzureCustomDomainOpsResource>());
        Assert.Contains(domainOps.Annotations.OfType<PipelineConfigurationAnnotation>(), _ => true);
    }

    [Fact]
    public async Task SecondProviderOfSameSlug_DoesNotDuplicatePrereqProviderStep()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });
        builder.AddAzureContainerAppEnvironment("aca-env");
        builder.AddDomainOpsProvider("dns1").Cloudflare();
        builder.AddDomainOpsProvider("dns2").Cloudflare();

        var steps = await CollectDomainOpsStepsAsync(builder);

        Assert.Single(steps, s => s.Name == "prereq-domain");
        Assert.Single(steps, s => s.Name == "prereq-domain-cloudflare");
    }

    [Fact]
    public async Task PullOctoDnsImageAsync_RunsDockerPull()
    {
        var runner = new RecordingProcessRunner();

        await DomainOpsProviderExtensions.PullOctoDnsImageAsync(
            runner,
            NullLogger.Instance,
            "octodns/ovh",
            CancellationToken.None);

        var command = Assert.Single(runner.Commands);
        Assert.Equal("docker", command.FileName);
        Assert.Equal(["pull", "octodns/ovh"], command.Arguments);
    }

    [Fact]
    public async Task PullOctoDnsImageAsync_ThrowsWhenPullFails()
    {
        var runner = new RecordingProcessRunner((_, _) => new ProcessResult(1, "", "boom"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DomainOpsProviderExtensions.PullOctoDnsImageAsync(
                runner,
                NullLogger.Instance,
                "octodns/cloudflare",
                CancellationToken.None));

        Assert.Contains("octodns/cloudflare", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    private static async Task<List<PipelineStep>> CollectDomainOpsStepsAsync(IDistributedApplicationBuilder builder)
    {
        var model = new DistributedApplicationModel([.. builder.Resources]);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var services = new ServiceCollection().BuildServiceProvider();
        var pipelineContext = new PipelineContext(
            model,
            executionContext,
            services,
            NullLogger.Instance,
            CancellationToken.None);

        var steps = new List<PipelineStep>();
        foreach (var resource in builder.Resources.Where(static r =>
                     r is AzureCustomDomainOpsResource or DomainOpsProviderResource))
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineStepAnnotation>())
            {
                var context = new PipelineStepFactoryContext
                {
                    Resource = resource,
                    PipelineContext = pipelineContext
                };
                var created = await annotation.CreateStepsAsync(context);
                steps.AddRange(created);
            }
        }

        return steps;
    }

    private sealed class RecordingProcessRunner(
        Func<string, IReadOnlyList<string>, ProcessResult>? handler = null) : IProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            Commands.Add((fileName, arguments.ToArray()));
            if (handler is not null)
            {
                return Task.FromResult(handler(fileName, arguments));
            }

            return Task.FromResult(new ProcessResult(0, "ok", string.Empty));
        }
    }
}
