using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging.Abstractions;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DomainOpsOrchestratorTests
{
    [Fact]
    public async Task GuardAsync_ThrowsWhenCertificateRequiredAndEmpty()
    {
        var orchestrator = CreateOrchestrator();
        var cert = CreateParameter("certificateName", "");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.GuardAsync(cert, new AzureCustomDomainOpsOptions { RequireCertificateName = true }, CancellationToken.None));
    }

    [Fact]
    public async Task GuardAsync_PassesWhenCertificatePresent()
    {
        var orchestrator = CreateOrchestrator();
        var cert = CreateParameter("certificateName", "my-cert");

        var outcome = await orchestrator.GuardAsync(
            cert,
            new AzureCustomDomainOpsOptions { RequireCertificateName = true },
            CancellationToken.None);

        Assert.False(outcome.Skipped);
        Assert.Equal("my-cert", outcome.CertificateName);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsSkippedDnsWhenNoPlanInput()
    {
        var orchestrator = CreateOrchestrator();
        var domain = CreateParameter("customDomain", "www.contoso.com");
        var cert = CreateParameter("certificateName", "my-cert");
        var resource = new TestResource("api");

        var outcome = await orchestrator.VerifyAsync(
            resource,
            domain,
            cert,
            new AzureCustomDomainOpsOptions { RequireCertificateName = true },
            CancellationToken.None);

        Assert.Equal("api", outcome.TargetResourceName);
        Assert.Equal("www.contoso.com", outcome.Hostname);
        Assert.Equal(DomainOpsDnsCheckStatus.SkippedNoPlanInput, outcome.DnsStatus);
    }

    [Fact]
    public async Task VerifyAsync_ThrowsOnDnsDrift()
    {
        var orchestrator = CreateOrchestrator();
        var domain = CreateParameter("customDomain", "www.contoso.com");
        var cert = CreateParameter("certificateName", "my-cert");
        var resource = new TestResource("api");

        var planInput = new DnsPlanInput(
            "www.contoso.com",
            "app.example.azurecontainerapps.io",
            "1.2.3.4",
            "code");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.VerifyAsync(
                resource,
                domain,
                cert,
                new AzureCustomDomainOpsOptions { RequireCertificateName = true },
                CancellationToken.None,
                observedRecords: [],
                planInput: planInput));
    }

    [Fact]
    public async Task VerifyAsync_PassesWhenObservedMatchesPlan()
    {
        var orchestrator = CreateOrchestrator();
        var domain = CreateParameter("customDomain", "www.contoso.com");
        var cert = CreateParameter("certificateName", "my-cert");
        var resource = new TestResource("api");

        var planInput = new DnsPlanInput(
            "www.contoso.com",
            "app.example.azurecontainerapps.io",
            "1.2.3.4",
            "code");

        var expected = new DnsRecordPlanner().Plan(planInput).Records;

        var outcome = await orchestrator.VerifyAsync(
            resource,
            domain,
            cert,
            new AzureCustomDomainOpsOptions { RequireCertificateName = true },
            CancellationToken.None,
            observedRecords: expected,
            planInput: planInput);

        Assert.Equal(DomainOpsDnsCheckStatus.Matched, outcome.DnsStatus);
        Assert.Equal(HostnameKind.Subdomain, outcome.Kind);
        Assert.Equal(expected.Count, outcome.PlannedRecordCount);
    }

    private static DomainOpsOrchestrator CreateOrchestrator()
        => new(new FakeProcessRunner(), NullLogger.Instance);

    private static ParameterResource CreateParameter(string name, string value)
        => new(name, _ => value, secret: false);

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
}
