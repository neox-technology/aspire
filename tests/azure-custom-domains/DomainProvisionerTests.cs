using Neox.Aspire.Hosting.Azure;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_RunsDockerOctoDnsHostnameBindAndGitHubVariableSet()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "neox-provision-" + Guid.NewGuid().ToString("N"));
        var zoneDir = Path.Combine(workDir, "zones");
        var configPath = Path.Combine(workDir, "octodns.yaml");
        Directory.CreateDirectory(zoneDir);

        try
        {
            var runner = new RecordingProcessRunner();
            var reader = new FakeAzureReader(new AzureContainerAppTargets(
                "api",
                "rg-demo",
                "aca-env",
                "api.nicehill.westeurope.azurecontainerapps.io",
                "20.1.2.3",
                "verification"));

            var provider = CreateCloudflareProvider("dns", "cf-token-secret");

            var provisioner = new DomainProvisioner(
                runner,
                reader,
                delayAsync: (_, _) => Task.CompletedTask);

            var certName = await provisioner.ProvisionAsync(
                "www.contoso.com",
                provider,
                new AzureCustomDomainOpsOptions
                {
                    ContainerAppResourceName = "api",
                    OctoDnsConfigPath = configPath,
                    OctoDnsZoneDirectory = zoneDir,
                    CertificateGitHubVariableName = "CERTIFICATE_NAME",
                    ManagedCertificateName = "www-contoso-com",
                    DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                    PollInterval = TimeSpan.Zero
                },
                CancellationToken.None);

            Assert.Equal("www-contoso-com", certName);

            var docker = Assert.Single(runner.Commands, c => c.FileName == "docker");
            Assert.Contains("run", docker.Arguments);
            Assert.Contains("octodns/cloudflare", docker.Arguments);
            Assert.Contains("octodns-sync", docker.Arguments);
            Assert.Contains("--doit", docker.Arguments);
            Assert.Contains("DNS_TOKEN", docker.Arguments);
            Assert.DoesNotContain(docker.Arguments, a => a.Contains("cf-token-secret", StringComparison.Ordinal));
            Assert.NotNull(docker.Environment);
            Assert.Equal("cf-token-secret", docker.Environment!["DNS_TOKEN"]);

            Assert.Contains(runner.Commands, c => c.FileName == "az" && c.Arguments.Contains("hostname") && c.Arguments.Contains("add"));
            Assert.Contains(runner.Commands, c => c.FileName == "az" && c.Arguments.Contains("bind") && c.Arguments.Contains("CNAME"));
            Assert.Contains(runner.Commands, c => c.FileName == "gh" && c.Arguments.Contains("CERTIFICATE_NAME") && c.Arguments.Contains("www-contoso-com"));
            Assert.True(File.Exists(Path.Combine(zoneDir, "contoso.com.yaml")));
            Assert.True(File.Exists(configPath));

            var configYaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("env/DNS_TOKEN", configYaml, StringComparison.Ordinal);
            Assert.DoesNotContain("cf-token-secret", configYaml, StringComparison.Ordinal);
            Assert.Contains("octodns_cloudflare.CloudflareProvider", configYaml, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProvisionAsync_UsesHttpValidationForApex()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "neox-provision-apex-" + Guid.NewGuid().ToString("N"));
        var zoneDir = Path.Combine(workDir, "zones");
        var configPath = Path.Combine(workDir, "octodns.yaml");
        Directory.CreateDirectory(zoneDir);

        try
        {
            var runner = new RecordingProcessRunner();
            var reader = new FakeAzureReader(new AzureContainerAppTargets(
                "api",
                "rg-demo",
                "aca-env",
                "api.nicehill.westeurope.azurecontainerapps.io",
                "20.1.2.3",
                "verification"));

            var provider = CreateCloudflareProvider("dns", "token");

            var provisioner = new DomainProvisioner(
                runner,
                reader,
                delayAsync: (_, _) => Task.CompletedTask);

            await provisioner.ProvisionAsync(
                "contoso.com",
                provider,
                new AzureCustomDomainOpsOptions
                {
                    ContainerAppResourceName = "api",
                    OctoDnsConfigPath = configPath,
                    OctoDnsZoneDirectory = zoneDir,
                    DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                    PollInterval = TimeSpan.Zero
                },
                CancellationToken.None);

            Assert.Contains(runner.Commands, c => c.FileName == "az" && c.Arguments.Contains("HTTP"));
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    private static CloudflareDomainOpsProviderResource CreateCloudflareProvider(string name, string tokenValue)
    {
        var provider = new CloudflareDomainOpsProviderResource(name);
        var parameter = new ParameterResource(provider.GetParameterName("token"), _ => tokenValue, secret: true);
        provider.BindAuthParameter("token", parameter);
        return provider;
    }

    private sealed class FakeAzureReader(AzureContainerAppTargets targets) : IAzureContainerAppReader
    {
        public Task<AzureContainerAppTargets> GetTargetsAsync(
            string containerAppName,
            string? resourceGroup,
            string? environmentName,
            CancellationToken cancellationToken)
            => Task.FromResult(targets);
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string>? Environment)> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            Commands.Add((fileName, arguments.ToArray(), environment));
            return Task.FromResult(new ProcessResult(0, "ok", string.Empty));
        }
    }
}
