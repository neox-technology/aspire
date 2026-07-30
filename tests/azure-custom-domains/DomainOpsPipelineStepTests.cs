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
        Assert.Equal("plan-domain-cloudflare", AzureCustomDomainOpsExtensions.GetDomainPlanProviderStepName("cloudflare"));
        Assert.Equal("plan-domain-contoso-com", AzureCustomDomainOpsExtensions.GetDomainPlanZoneStepName("contoso.com"));
        Assert.Equal("provision-domain-contoso-com", AzureCustomDomainOpsExtensions.GetDomainProvisionZoneStepName("contoso.com"));
        Assert.Equal("plan-aca-env-certificates", AzureCustomDomainOpsExtensions.GetPlanEnvCertificatesStepName("aca-env"));
        Assert.Equal("provision-aca-env-certificates", AzureCustomDomainOpsExtensions.GetProvisionEnvCertificatesStepName("aca-env"));
        Assert.Equal("provision-aca-env-domains", AzureCustomDomainOpsExtensions.GetProvisionEnvDomainsStepName("aca-env"));
        Assert.Equal(
            "plan-api-domain-www-example-com",
            AzureCustomDomainOpsExtensions.GetDomainPlanResourceStepName("api", "www-example-com"));
        Assert.Equal(
            "provision-api-domain-www-example-com",
            AzureCustomDomainOpsExtensions.GetDomainProvisionStepName("api", "www-example-com"));
        Assert.Equal(
            "deploy-api-domain-www-example-com",
            AzureCustomDomainOpsExtensions.GetDomainDeployStepName("api", "www-example-com"));
        Assert.Equal("deploy-domains", AzureCustomDomainOpsExtensions.DeployDomainsStepName);
        Assert.Equal("provision-api-containerapp", AzureCustomDomainOpsExtensions.GetContainerAppProvisionStepName("api"));
        Assert.Equal("contoso-com", AzureCustomDomainOpsExtensions.ToZoneSlug("contoso.com"));
        Assert.Equal("www-example-com", AzureCustomDomainOpsExtensions.ToZoneSlug("www.example.com"));
    }

    [Fact]
    public async Task RegistersSplitPlanAndProvisionSteps_WithExpectedDependsOn()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var customDomain = builder.AddParameter("customDomain", "www.example.com");
        var certificateName = builder.AddParameter("certificateName");
        var dns = builder.AddDomainOpsProvider("dns").Ovh();

        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps(customDomain, certificateName, dns, o => o.DnsZoneName = "example.com");

        var steps = await CollectDomainOpsStepsAsync(builder);

        var prereqDomain = Assert.Single(steps, s => s.Name == "prereq-domain");
        Assert.Contains("provision-aca-env", prereqDomain.DependsOnSteps);

        var prereqOvh = Assert.Single(steps, s => s.Name == "prereq-domain-ovh");
        Assert.Contains("prereq-domain", prereqOvh.DependsOnSteps);

        var planProvider = Assert.Single(steps, s => s.Name == "plan-domain-ovh");
        Assert.Contains("prereq-domain-ovh", planProvider.DependsOnSteps);

        var planZone = Assert.Single(steps, s => s.Name == "plan-domain-example-com");
        Assert.Contains("plan-domain-ovh", planZone.DependsOnSteps);

        var provisionZone = Assert.Single(steps, s => s.Name == "provision-domain-example-com");
        Assert.Contains("plan-domain-example-com", provisionZone.DependsOnSteps);

        Assert.Contains(steps, s => s.Name == "plan-aca-env-certificates");
        Assert.Contains(steps, s => s.Name == "provision-aca-env-certificates");
        Assert.Contains(steps, s => s.Name == "provision-aca-env-domains");
        Assert.Contains(steps, s => s.Name == "plan-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-domains");

        var provisionDomain = Assert.Single(steps, s => s.Name == "provision-api-domain-www-example-com");
        Assert.Contains("plan-api-domain-www-example-com", provisionDomain.DependsOnSteps);
        Assert.Contains("provision-domain-example-com", provisionDomain.DependsOnSteps);
        Assert.DoesNotContain("provision-aca-env-certificates", provisionDomain.DependsOnSteps);

        var deployDomain = Assert.Single(steps, s => s.Name == "deploy-api-domain-www-example-com");
        Assert.Contains("provision-aca-env-certificates", deployDomain.DependsOnSteps);
        Assert.Contains("deploy-domains", deployDomain.RequiredBySteps);
        Assert.DoesNotContain("provision-api-containerapp", deployDomain.DependsOnSteps);

        var deployDomains = Assert.Single(steps, s => s.Name == "deploy-domains");
        Assert.Contains(WellKnownPipelineSteps.Deploy, deployDomains.RequiredBySteps);

        Assert.DoesNotContain(steps, s => s.Name == "domain-verify");
        Assert.DoesNotContain(steps, s => s.Name == "domain-guard");
        Assert.DoesNotContain(steps, s => s.Name == "domain-provision");

        var domainOps = Assert.Single(builder.Resources.OfType<AzureCustomDomainOpsResource>());
        Assert.Contains(domainOps.Annotations.OfType<PipelineConfigurationAnnotation>(), _ => true);
    }

    [Fact]
    public async Task SecondBindingSameZone_DoesNotDuplicateZoneSteps()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var domain1 = builder.AddParameter("customDomain1", "www.example.com");
        var domain2 = builder.AddParameter("customDomain2", "api.example.com");
        var cert1 = builder.AddParameter("certificateName1");
        var cert2 = builder.AddParameter("certificateName2");
        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();
        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps(domain1, cert1, dns, o => o.DnsZoneName = "example.com");

        builder.AddContainer("web", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps(domain2, cert2, dns, o => o.DnsZoneName = "example.com");

        var steps = await CollectDomainOpsStepsAsync(builder);

        Assert.Single(steps, s => s.Name == "plan-domain-example-com");
        Assert.Single(steps, s => s.Name == "provision-domain-example-com");
        Assert.Single(steps, s => s.Name == "plan-domain-cloudflare");
        Assert.Contains(steps, s => s.Name == "plan-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "plan-web-domain-api-example-com");
        Assert.Contains(steps, s => s.Name == "provision-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "provision-web-domain-api-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-web-domain-api-example-com");
        Assert.Single(steps, s => s.Name == "provision-aca-env-domains");
        Assert.Single(steps, s => s.Name == "deploy-domains");
    }

    [Fact]
    public async Task SecondBindingSameResource_RegistersDistinctDomainSteps()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var www = builder.AddParameter("customDomain", "www.example.com");
        var apex = builder.AddParameter("customApexDomain", "example.com");
        var wwwCert = builder.AddParameter("certificateName");
        var apexCert = builder.AddParameter("certificateApexName");
        var dns = builder.AddDomainOpsProvider("dns").Ovh();
        builder.AddAzureContainerAppEnvironment("aca-env");

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithHttpEndpoint(targetPort: 8080)
            .PublishAsAzureContainerApp((_, _) => { })
            .WithAzureCustomDomainOps(www, wwwCert, dns, o => o.DnsZoneName = "example.com")
            .WithAzureCustomDomainOps(apex, apexCert, dns, o => o.DnsZoneName = "example.com");

        var steps = await CollectDomainOpsStepsAsync(builder);

        Assert.Single(steps, s => s.Name == "plan-domain-example-com");
        Assert.Single(steps, s => s.Name == "provision-domain-example-com");
        Assert.Contains(steps, s => s.Name == "plan-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "plan-api-domain-example-com");
        Assert.Contains(steps, s => s.Name == "provision-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "provision-api-domain-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-api-domain-www-example-com");
        Assert.Contains(steps, s => s.Name == "deploy-api-domain-example-com");
        Assert.Single(steps, s => s.Name == "provision-aca-env-domains");
        Assert.Single(steps, s => s.Name == "deploy-domains");
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
