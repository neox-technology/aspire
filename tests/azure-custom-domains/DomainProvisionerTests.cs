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
    public async Task ProvisionAsync_DumpsUpsertsDryRunsThenAppliesWithoutDeletes()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "neox-provision-" + Guid.NewGuid().ToString("N"));
        var zoneDir = Path.Combine(workDir, "zones");
        var configPath = Path.Combine(workDir, "octodns.yaml");
        Directory.CreateDirectory(zoneDir);

        try
        {
            var runner = new RecordingProcessRunner((fileName, args) =>
            {
                if (fileName == "docker" && args.Contains("octodns-dump"))
                {
                    File.WriteAllText(Path.Combine(zoneDir, "contoso.com.yaml"), """
                        ---
                        '':
                          - ttl: 3600
                            type: MX
                            value: 10 mx.contoso.com.
                        other:
                          - ttl: 300
                            type: TXT
                            value: keep-me
                        """);
                    return new ProcessResult(0, "dumped", string.Empty);
                }

                if (fileName == "docker" && args.Contains("octodns-sync") && !args.Contains("--doit"))
                {
                    return new ProcessResult(
                        0,
                        "Summary: Creates=2, Updates=0, Deletes=0, Existing=4, Meta=False",
                        string.Empty);
                }

                return new ProcessResult(0, "ok", string.Empty);
            });

            var azure = new FakeAzureClient(new AzureContainerAppTargets(
                "api",
                "rg-demo",
                "aca-env",
                "api.nicehill.westeurope.azurecontainerapps.io",
                "20.1.2.3",
                "verification"));

            var provider = CreateCloudflareProvider("dns", "cf-token-secret");

            var provisioner = new DomainProvisioner(
                runner,
                azure,
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

            var dockerCommands = runner.Commands.Where(c => c.FileName == "docker").ToList();
            Assert.Equal(3, dockerCommands.Count);
            Assert.Contains("octodns-dump", dockerCommands[0].Arguments);
            Assert.Contains("--lenient", dockerCommands[0].Arguments);
            Assert.Contains("octodns-sync", dockerCommands[1].Arguments);
            Assert.DoesNotContain("--doit", dockerCommands[1].Arguments);
            Assert.Contains("octodns-sync", dockerCommands[2].Arguments);
            Assert.Contains("--doit", dockerCommands[2].Arguments);
            Assert.Contains("octodns/cloudflare", dockerCommands[2].Arguments);
            Assert.Contains("DNS_TOKEN", dockerCommands[2].Arguments);
            Assert.DoesNotContain(dockerCommands[2].Arguments, a => a.Contains("cf-token-secret", StringComparison.Ordinal));
            Assert.Equal("cf-token-secret", dockerCommands[2].Environment!["DNS_TOKEN"]);

            Assert.DoesNotContain(runner.Commands, c => c.FileName == "az");
            Assert.Single(azure.Binds);
            Assert.Equal("www.contoso.com", azure.Binds[0].Hostname);
            Assert.Equal("www-contoso-com", azure.Binds[0].CertificateName);
            Assert.Equal("CNAME", azure.Binds[0].ValidationMethod);
            Assert.Contains(runner.Commands, c => c.FileName == "gh" && c.Arguments.Contains("CERTIFICATE_NAME") && c.Arguments.Contains("www-contoso-com"));

            var zoneYaml = await File.ReadAllTextAsync(Path.Combine(zoneDir, "contoso.com.yaml"));
            Assert.Contains("type: MX", zoneYaml, StringComparison.Ordinal);
            Assert.Contains("keep-me", zoneYaml, StringComparison.Ordinal);
            Assert.Contains("type: CNAME", zoneYaml, StringComparison.Ordinal);
            Assert.Contains("asuid.www", zoneYaml, StringComparison.Ordinal);

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
            var runner = new RecordingProcessRunner((_, args) =>
            {
                if (args.Contains("octodns-sync") && !args.Contains("--doit"))
                {
                    return new ProcessResult(
                        0,
                        "Summary: Creates=2, Updates=0, Deletes=0, Existing=0, Meta=False",
                        string.Empty);
                }

                return new ProcessResult(0, "ok", string.Empty);
            });

            var azure = new FakeAzureClient(new AzureContainerAppTargets(
                "api",
                "rg-demo",
                "aca-env",
                "api.nicehill.westeurope.azurecontainerapps.io",
                "20.1.2.3",
                "verification"));

            var provider = CreateCloudflareProvider("dns", "token");

            var provisioner = new DomainProvisioner(
                runner,
                azure,
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

            Assert.DoesNotContain(runner.Commands, c => c.FileName == "az");
            Assert.Equal("HTTP", Assert.Single(azure.Binds).ValidationMethod);
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
    public async Task ProvisionAsync_AbortsWhenDryRunPlanContainsDeletes()
    {
        var workDir = Path.Combine(Path.GetTempPath(), "neox-provision-deletes-" + Guid.NewGuid().ToString("N"));
        var zoneDir = Path.Combine(workDir, "zones");
        var configPath = Path.Combine(workDir, "octodns.yaml");
        Directory.CreateDirectory(zoneDir);

        try
        {
            var runner = new RecordingProcessRunner((_, args) =>
            {
                if (args.Contains("octodns-sync") && !args.Contains("--doit"))
                {
                    return new ProcessResult(
                        0,
                        "Summary: Creates=2, Updates=0, Deletes=3, Existing=5, Meta=False",
                        string.Empty);
                }

                return new ProcessResult(0, "ok", string.Empty);
            });

            var azure = new FakeAzureClient(new AzureContainerAppTargets(
                "api",
                "rg-demo",
                "aca-env",
                "api.nicehill.westeurope.azurecontainerapps.io",
                "20.1.2.3",
                "verification"));

            var provisioner = new DomainProvisioner(
                runner,
                azure,
                delayAsync: (_, _) => Task.CompletedTask);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync(
                "www.contoso.com",
                CreateCloudflareProvider("dns", "token"),
                new AzureCustomDomainOpsOptions
                {
                    ContainerAppResourceName = "api",
                    OctoDnsConfigPath = configPath,
                    OctoDnsZoneDirectory = zoneDir,
                    DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                    PollInterval = TimeSpan.Zero
                },
                CancellationToken.None));

            Assert.Contains("upsert-only", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(runner.Commands, c => c.FileName == "docker" && c.Arguments.Contains("--doit"));
            Assert.Empty(azure.Binds);
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
    public void EnsureUpsertOnlyPlan_AllowsZeroDeletesAndNoChanges()
    {
        DomainProvisioner.EnsureUpsertOnlyPlan("Summary: Creates=1, Updates=1, Deletes=0, Existing=4, Meta=False");
        DomainProvisioner.EnsureUpsertOnlyPlan("No changes were planned");
    }

    [Fact]
    public void EnsureUpsertOnlyPlan_RejectsDeletes()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DomainProvisioner.EnsureUpsertOnlyPlan("Summary: Creates=0, Updates=0, Deletes=2, Existing=4, Meta=False"));
    }

    private static CloudflareDomainOpsProviderResource CreateCloudflareProvider(string name, string tokenValue)
    {
        var provider = new CloudflareDomainOpsProviderResource(name);
        var parameter = new ParameterResource(provider.GetParameterName("token"), _ => tokenValue, secret: true);
        provider.BindAuthParameter("token", parameter);
        return provider;
    }

    private sealed class FakeAzureClient(AzureContainerAppTargets targets) : IAzureContainerAppClient
    {
        public List<(string Hostname, string CertificateName, string ValidationMethod)> Binds { get; } = [];

        public Task<AzureContainerAppTargets> GetTargetsAsync(
            string containerAppName,
            string? resourceGroup,
            string? environmentName,
            CancellationToken cancellationToken)
            => Task.FromResult(targets);

        public Task BindManagedHostnameAsync(
            AzureContainerAppTargets targets,
            string hostname,
            string certificateName,
            string validationMethod,
            CancellationToken cancellationToken)
        {
            Binds.Add((hostname, certificateName, validationMethod));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingProcessRunner(
        Func<string, IReadOnlyList<string>, ProcessResult>? handler = null) : IProcessRunner
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
            if (handler is not null)
            {
                return Task.FromResult(handler(fileName, arguments));
            }

            return Task.FromResult(new ProcessResult(0, "ok", string.Empty));
        }
    }
}
