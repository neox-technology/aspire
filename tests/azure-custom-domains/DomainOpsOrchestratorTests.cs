using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsOrchestratorTests
{
    [Fact]
    public void PlanResourceDomain_BuildsModelWithoutArm()
    {
        var orchestrator = CreateOrchestrator();
        var resource = new TestResource("api");

        var plan = orchestrator.PlanResourceDomain(
            resource,
            "www.contoso.com",
            new AzureCustomDomainOpsOptions { ManagedCertificateName = "www-contoso-com" },
            certificateNameParameter: null);

        Assert.Equal("api", plan.TargetResourceName);
        Assert.Equal("www.contoso.com", plan.Hostname);
        Assert.Equal("www-contoso-com", plan.CertificateName);
        Assert.Equal("CNAME", plan.ValidationMethod);
        Assert.Equal(HostnameKind.Subdomain, plan.Kind);
    }

    [Fact]
    public void PlanResourceDomain_UsesHttpForApex()
    {
        var orchestrator = CreateOrchestrator();

        var plan = orchestrator.PlanResourceDomain(
            new TestResource("api"),
            "contoso.com",
            new AzureCustomDomainOpsOptions(),
            certificateNameParameter: null);

        Assert.Equal("HTTP", plan.ValidationMethod);
        Assert.Equal(HostnameKind.Apex, plan.Kind);
    }

    [Fact]
    public void PlanProvider_WritesOctoDnsConfig()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "neox-plan-provider-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            var configPath = Path.Combine(workDir, "octodns.yaml");
            var zoneDir = Path.Combine(workDir, "zones");
            Directory.CreateDirectory(zoneDir);

            var provider = new CloudflareDomainOpsProviderResource("dns");
            provider.BindAuthParameter("token", new ParameterResource("dns-token", _ => "secret", secret: true));

            var orchestrator = CreateOrchestrator();
            var outcome = orchestrator.PlanProvider(
                provider,
                ["contoso.com"],
                new AzureCustomDomainOpsOptions
                {
                    OctoDnsConfigPath = configPath,
                    OctoDnsZoneDirectory = zoneDir
                });

            Assert.Equal(configPath, outcome.ConfigPath);
            Assert.Contains("env/DNS_TOKEN", File.ReadAllText(configPath), StringComparison.Ordinal);
            Assert.DoesNotContain("secret", File.ReadAllText(configPath), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    private static DomainOpsOrchestrator CreateOrchestrator()
        => new(
            new FakeProcessRunner(),
            NullLogger.Instance,
            new DnsRecordPlanner(),
            new OctoDnsZoneWriter(),
            new OctoDnsConfigWriter(),
            azureClient: new FakeAzureClient(),
            provisioner: null);

    private sealed class TestResource(string name) : Resource(name);

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string>? environment = null)
            => Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }

    private sealed class FakeAzureClient : IAzureContainerAppClient
    {
        public Task<AzureContainerAppTargets> GetTargetsAsync(
            string containerAppName,
            string? resourceGroup,
            string? environmentName,
            CancellationToken cancellationToken)
            => Task.FromResult(new AzureContainerAppTargets(
                containerAppName,
                resourceGroup ?? "rg",
                environmentName ?? "env",
                "app.example.azurecontainerapps.io",
                "1.2.3.4",
                "asuid"));

        public Task<IReadOnlyList<AzureManagedCertificateInfo>> ListManagedCertificatesAsync(
            AzureContainerAppTargets targets,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AzureManagedCertificateInfo>>([]);

        public Task<AzureManagedCertificateInfo> CreateManagedCertificateAsync(
            AzureContainerAppTargets targets,
            string hostname,
            string certificateName,
            string validationMethod,
            CancellationToken cancellationToken)
            => Task.FromResult(new AzureManagedCertificateInfo(certificateName, hostname, $"/certs/{certificateName}"));

        public Task BindHostnameAsync(
            AzureContainerAppTargets targets,
            string hostname,
            string certificateId,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
