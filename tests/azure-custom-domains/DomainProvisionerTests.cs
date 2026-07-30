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
    public async Task PlanAndProvisionZone_DumpsUpsertsDryRunsThenAppliesWithoutDeletes()
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
            var options = new AzureCustomDomainOpsOptions
            {
                ContainerAppResourceName = "api",
                OctoDnsConfigPath = configPath,
                OctoDnsZoneDirectory = zoneDir,
                ManagedCertificateName = "www-contoso-com",
                DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                PollInterval = TimeSpan.Zero
            };

            var provisioner = new DomainProvisioner(
                runner,
                azure,
                delayAsync: (_, _) => Task.CompletedTask);

            provisioner.PlanProviderConfig(provider, ["contoso.com"], options);

            await provisioner.PlanZoneAsync(
                "contoso.com",
                [new ZoneBindingInput("www.contoso.com", "api")],
                provider,
                options,
                CancellationToken.None);

            var waitPlan = new DnsRecordPlanner().Plan(new DnsPlanInput(
                "www.contoso.com",
                azure.Targets.Fqdn,
                azure.Targets.StaticIp,
                azure.Targets.CustomDomainVerificationId));

            await provisioner.ProvisionZoneAsync(
                "contoso.com",
                provider,
                options,
                waitPlan,
                CancellationToken.None);

            var dockerCommands = runner.Commands.Where(c => c.FileName == "docker").ToList();
            Assert.Equal(3, dockerCommands.Count);
            Assert.Contains("octodns-dump", dockerCommands[0].Arguments);
            Assert.Contains("--lenient", dockerCommands[0].Arguments);
            Assert.Contains("octodns-sync", dockerCommands[1].Arguments);
            Assert.DoesNotContain("--doit", dockerCommands[1].Arguments);
            Assert.Contains("octodns-sync", dockerCommands[2].Arguments);
            Assert.Contains("--doit", dockerCommands[2].Arguments);
            Assert.Contains("octodns/cloudflare", dockerCommands[2].Arguments);
            Assert.Equal("cf-token-secret", dockerCommands[2].Environment!["DNS_TOKEN"]);

            Assert.DoesNotContain(runner.Commands, c => c.FileName == "az");
            Assert.DoesNotContain(runner.Commands, c => c.FileName == "gh");

            var zoneYaml = await File.ReadAllTextAsync(Path.Combine(zoneDir, "contoso.com.yaml"));
            Assert.Contains("type: MX", zoneYaml, StringComparison.Ordinal);
            Assert.Contains("keep-me", zoneYaml, StringComparison.Ordinal);
            Assert.Contains("type: A", zoneYaml, StringComparison.Ordinal);
            Assert.DoesNotContain("type: CNAME", zoneYaml, StringComparison.Ordinal);

            var configYaml = await File.ReadAllTextAsync(configPath);
            Assert.Contains("env/DNS_TOKEN", configYaml, StringComparison.Ordinal);
            Assert.DoesNotContain("cf-token-secret", configYaml, StringComparison.Ordinal);
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
    public async Task ProvisionEnvCertificates_CreatesMissingThenBindUsesCertificate()
    {
        var runner = new RecordingProcessRunner();
        var azure = new FakeAzureClient(new AzureContainerAppTargets(
            "api",
            "rg-demo",
            "aca-env",
            "api.nicehill.westeurope.azurecontainerapps.io",
            "20.1.2.3",
            "verification"));

        var provisioner = new DomainProvisioner(runner, azure, delayAsync: (_, _) => Task.CompletedTask);
        var plan = provisioner.PlanResourceDomain(
            "api",
            "www.contoso.com",
            new AzureCustomDomainOpsOptions { ManagedCertificateName = "www-contoso-com" },
            certificateNameParameter: null);

        Assert.Equal("HTTP", plan.ValidationMethod);

        await provisioner.ProvisionEnvCertificatesAsync(
            azure.Targets,
            [plan],
            existingCertificates: [],
            CancellationToken.None);

        Assert.Single(azure.Created);
        Assert.Equal("www-contoso-com", azure.Created[0].Name);

        var certs = await azure.ListManagedCertificatesAsync(azure.Targets, CancellationToken.None);
        await provisioner.BindResourceDomainAsync(azure.Targets, plan, certs, CancellationToken.None);

        Assert.Single(azure.Binds);
        Assert.Equal("www.contoso.com", azure.Binds[0].Hostname);
        Assert.Contains("www-contoso-com", azure.Binds[0].CertificateId, StringComparison.Ordinal);
        Assert.DoesNotContain(runner.Commands, c => c.FileName == "gh");
    }

    [Fact]
    public async Task EnsureResourceHostname_AddsOnceThenNoOps()
    {
        var runner = new RecordingProcessRunner();
        var azure = new FakeAzureClient(new AzureContainerAppTargets(
            "api",
            "rg-demo",
            "aca-env",
            "api.nicehill.westeurope.azurecontainerapps.io",
            "20.1.2.3",
            "verification"));

        var provisioner = new DomainProvisioner(runner, azure, delayAsync: (_, _) => Task.CompletedTask);
        var plan = provisioner.PlanResourceDomain(
            "api",
            "www.contoso.com",
            new AzureCustomDomainOpsOptions { ManagedCertificateName = "www-contoso-com" },
            certificateNameParameter: null);

        Assert.True(await provisioner.EnsureResourceHostnameAsync(azure.Targets, plan, CancellationToken.None));
        Assert.False(await provisioner.EnsureResourceHostnameAsync(azure.Targets, plan, CancellationToken.None));
        Assert.Equal(["www.contoso.com"], azure.Ensured);
        Assert.Empty(azure.Binds);
    }

    [Fact]
    public async Task ProvisionZone_AbortsWhenDryRunPlanContainsDeletes()
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

            var options = new AzureCustomDomainOpsOptions
            {
                ContainerAppResourceName = "api",
                OctoDnsConfigPath = configPath,
                OctoDnsZoneDirectory = zoneDir,
                DnsPropagationTimeout = TimeSpan.FromSeconds(1),
                PollInterval = TimeSpan.Zero
            };

            provisioner.PlanProviderConfig(CreateCloudflareProvider("dns", "token"), ["contoso.com"], options);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionZoneAsync(
                "contoso.com",
                CreateCloudflareProvider("dns", "token"),
                options,
                waitPlan: null,
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
        public AzureContainerAppTargets Targets { get; } = targets;
        public List<AzureManagedCertificateInfo> Created { get; } = [];
        public List<(string Hostname, string CertificateId)> Binds { get; } = [];
        public HashSet<string> Hostnames { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Ensured { get; } = [];

        public Task<AzureContainerAppTargets> GetTargetsAsync(
            string containerAppName,
            string? resourceGroup,
            string? environmentName,
            CancellationToken cancellationToken)
            => Task.FromResult(Targets);

        public Task<IReadOnlyList<AzureManagedCertificateInfo>> ListManagedCertificatesAsync(
            AzureContainerAppTargets targets,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AzureManagedCertificateInfo>>(Created.ToList());

        public Task<AzureManagedCertificateInfo> CreateManagedCertificateAsync(
            AzureContainerAppTargets targets,
            string hostname,
            string certificateName,
            string validationMethod,
            CancellationToken cancellationToken)
        {
            var info = new AzureManagedCertificateInfo(certificateName, hostname, $"/subscriptions/x/certs/{certificateName}");
            Created.Add(info);
            return Task.FromResult(info);
        }

        public Task BindHostnameAsync(
            AzureContainerAppTargets targets,
            string hostname,
            string certificateId,
            CancellationToken cancellationToken)
        {
            Binds.Add((hostname, certificateId));
            Hostnames.Add(hostname);
            return Task.CompletedTask;
        }

        public Task<bool> EnsureHostnameAsync(
            AzureContainerAppTargets targets,
            string hostname,
            CancellationToken cancellationToken)
        {
            if (Hostnames.Contains(hostname))
            {
                return Task.FromResult(false);
            }

            Hostnames.Add(hostname);
            Ensured.Add(hostname);
            return Task.FromResult(true);
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
