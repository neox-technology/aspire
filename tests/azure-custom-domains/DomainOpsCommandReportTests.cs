using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neox.Aspire.Hosting.Azure.Dns;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

#pragma warning disable ASPIREINTERACTION001

public sealed class DomainOpsCommandReportTests
{
    [Fact]
    public void ToMarkdown_Verify_IncludesProviderAndBindingDetails()
    {
        var report = new DomainOpsCommandReport(DomainOpsActionKind.Verify, "dns");
        report.AddVerify(new DomainOpsVerifyOutcome(
            "api",
            "www.contoso.com",
            DomainOpsDnsCheckStatus.Matched,
            HostnameKind.Subdomain,
            PlannedRecordCount: 3));

        var markdown = report.ToMarkdown();

        Assert.Contains("# DomainOps Verify", markdown);
        Assert.Contains("**Provider:** `dns`", markdown);
        Assert.Contains("## Binding: `api`", markdown);
        Assert.Contains("`www.contoso.com`", markdown);
        Assert.Contains("3 record(s) match plan (Subdomain)", markdown);
        Assert.Equal("Completed Verify for 1 binding(s) on provider 'dns'.", report.SummaryMessage);
    }

    [Fact]
    public void ToMarkdown_Failure_AppendsExceptionDetail()
    {
        var report = new DomainOpsCommandReport(DomainOpsActionKind.Guard, "dns");
        report.AddGuard("api", new DomainOpsGuardOutcome(Skipped: false, CertificateName: "cert"));
        report.SetFailure("web", new InvalidOperationException("Certificate name parameter is empty."));

        var markdown = report.ToMarkdown();

        Assert.Contains("## Failure", markdown);
        Assert.Contains("binding `web`", markdown);
        Assert.Contains("Certificate name parameter is empty.", markdown);
        Assert.Equal("Guard failed for provider 'dns'.", report.SummaryMessage);
    }

    [Fact]
    public async Task VerifyCommand_ReturnsMarkdownResultData()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomain = builder.AddParameter("customDomain", "www.contoso.com");
        var certificateName = builder.AddParameter("certificateName", "my-cert");
        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain, certificateName, dns, options =>
            {
                options.RequireCertificateName = true;
            });

        var command = dns.Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Single(a => a.Name == AzureCustomDomainOpsExtensions.DomainVerifyStepName);

        var services = new ServiceCollection();
        services.AddSingleton(new DistributedApplicationModel(builder.Resources));
        await using var provider = services.BuildServiceProvider();

        var context = new ExecuteCommandContext
        {
            ServiceProvider = provider,
            ResourceName = dns.Resource.Name,
            CancellationToken = CancellationToken.None,
            Logger = NullLogger.Instance,
            Arguments = new InteractionInputCollection([])
        };

        var result = await command.ExecuteCommand(context);

        Assert.True(result.Success);
        Assert.Contains("Completed Verify", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(CommandResultFormat.Markdown, result.Data.Format);
        Assert.True(result.Data.DisplayImmediately);
        Assert.Contains("# DomainOps Verify", result.Data.Value);
        Assert.Contains("## Binding: `api`", result.Data.Value);
        Assert.Contains("skipped (no ACA plan input)", result.Data.Value);
    }

    [Fact]
    public async Task GuardCommand_ReturnsMarkdownWithoutDisplayImmediately()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomain = builder.AddParameter("customDomain", "www.contoso.com");
        var certificateName = builder.AddParameter("certificateName", "my-cert");
        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain, certificateName, dns);

        var command = dns.Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Single(a => a.Name == AzureCustomDomainOpsExtensions.DomainGuardStepName);

        var services = new ServiceCollection();
        services.AddSingleton(new DistributedApplicationModel(builder.Resources));
        await using var provider = services.BuildServiceProvider();

        var result = await command.ExecuteCommand(new ExecuteCommandContext
        {
            ServiceProvider = provider,
            ResourceName = dns.Resource.Name,
            CancellationToken = CancellationToken.None,
            Logger = NullLogger.Instance,
            Arguments = new InteractionInputCollection([])
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(CommandResultFormat.Markdown, result.Data.Format);
        Assert.False(result.Data.DisplayImmediately);
        Assert.Contains("# DomainOps Guard", result.Data.Value);
        Assert.Contains("certificate `my-cert`", result.Data.Value);
    }

    [Fact]
    public async Task GuardCommand_Failure_IncludesMarkdownPayload()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customDomain = builder.AddParameter("customDomain", "www.contoso.com");
        var certificateName = builder.AddParameter("certificateName", "");
        var dns = builder.AddDomainOpsProvider("dns").Cloudflare();

        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithAzureCustomDomainOps(customDomain, certificateName, dns, options =>
            {
                options.RequireCertificateName = true;
            });

        var command = dns.Resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Single(a => a.Name == AzureCustomDomainOpsExtensions.DomainGuardStepName);

        var services = new ServiceCollection();
        services.AddSingleton(new DistributedApplicationModel(builder.Resources));
        await using var provider = services.BuildServiceProvider();

        var result = await command.ExecuteCommand(new ExecuteCommandContext
        {
            ServiceProvider = provider,
            ResourceName = dns.Resource.Name,
            CancellationToken = CancellationToken.None,
            Logger = NullLogger.Instance,
            Arguments = new InteractionInputCollection([])
        });

        Assert.False(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(CommandResultFormat.Markdown, result.Data.Format);
        Assert.Contains("## Failure", result.Data.Value);
        Assert.Contains("Certificate name parameter is empty", result.Data.Value);
    }
}

#pragma warning restore ASPIREINTERACTION001
