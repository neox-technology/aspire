using Neox.Aspire.Hosting.Azure.Processes;
using Neox.Aspire.Hosting.Azure.Provisioning;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainProvisionerTests
{
    [Fact]
    public async Task ProvisionAsync_RunsOctoDnsHostnameBindAndGitHubVariableSet()
    {
        var zoneDir = Path.Combine(Path.GetTempPath(), "neox-provision-" + Guid.NewGuid().ToString("N"));
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

            var provisioner = new DomainProvisioner(
                runner,
                reader,
                delayAsync: (_, _) => Task.CompletedTask);

            var certName = await provisioner.ProvisionAsync(
                "www.contoso.com",
                new AzureCustomDomainOpsOptions
                {
                    ContainerAppResourceName = "api",
                    OctoDnsConfigPath = "octodns.yaml",
                    OctoDnsZoneDirectory = zoneDir,
                    CertificateGitHubVariableName = "CERTIFICATE_NAME",
                    ManagedCertificateName = "www-contoso-com",
                    DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                    PollInterval = TimeSpan.Zero
                },
                CancellationToken.None);

            Assert.Equal("www-contoso-com", certName);
            Assert.Contains(runner.Commands, c => c.FileName == "octodns-sync");
            Assert.Contains(runner.Commands, c => c.FileName == "az" && c.Arguments.Contains("hostname") && c.Arguments.Contains("add"));
            Assert.Contains(runner.Commands, c => c.FileName == "az" && c.Arguments.Contains("bind") && c.Arguments.Contains("CNAME"));
            Assert.Contains(runner.Commands, c => c.FileName == "gh" && c.Arguments.Contains("CERTIFICATE_NAME") && c.Arguments.Contains("www-contoso-com"));
            Assert.True(File.Exists(Path.Combine(zoneDir, "contoso.com.yaml")));
        }
        finally
        {
            if (Directory.Exists(zoneDir))
            {
                Directory.Delete(zoneDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProvisionAsync_UsesHttpValidationForApex()
    {
        var zoneDir = Path.Combine(Path.GetTempPath(), "neox-provision-apex-" + Guid.NewGuid().ToString("N"));
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

            var provisioner = new DomainProvisioner(
                runner,
                reader,
                delayAsync: (_, _) => Task.CompletedTask);

            await provisioner.ProvisionAsync(
                "contoso.com",
                new AzureCustomDomainOpsOptions
                {
                    ContainerAppResourceName = "api",
                    OctoDnsZoneDirectory = zoneDir,
                    DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                    PollInterval = TimeSpan.Zero
                },
                CancellationToken.None);

            Assert.Contains(runner.Commands, c => c.FileName == "az" && c.Arguments.Contains("HTTP"));
        }
        finally
        {
            if (Directory.Exists(zoneDir))
            {
                Directory.Delete(zoneDir, recursive: true);
            }
        }
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
        public List<(string FileName, IReadOnlyList<string> Arguments)> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken,
            string? workingDirectory = null,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            Commands.Add((fileName, arguments.ToArray()));
            return Task.FromResult(new ProcessResult(0, "ok", string.Empty));
        }
    }
}
