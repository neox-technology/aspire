using Neox.Aspire.Hosting.Azure.Dns;
using Xunit;

namespace Neox.Aspire.Hosting.Azure.CustomDomains.Tests;

public sealed class DnsRecordPlannerTests
{
    private readonly DnsRecordPlanner _planner = new();

    [Fact]
    public void Plan_Apex_EmitsAAndAsuidTxt()
    {
        var plan = _planner.Plan(new DnsPlanInput(
            "contoso.com",
            "api.nicehill-1234.westeurope.azurecontainerapps.io",
            "20.50.1.2",
            "verification-code"));

        Assert.Equal(HostnameKind.Apex, plan.Kind);
        Assert.Equal("contoso.com", plan.ZoneName);
        Assert.Contains(plan.Records, r => r.Type == "A" && r.Name == "" && r.Value == "20.50.1.2");
        Assert.Contains(plan.Records, r => r.Type == "TXT" && r.Name == "asuid" && r.Value == "verification-code");
    }

    [Fact]
    public void Plan_Subdomain_EmitsCnameAndAsuidTxt()
    {
        var plan = _planner.Plan(new DnsPlanInput(
            "www.contoso.com",
            "api.nicehill-1234.westeurope.azurecontainerapps.io",
            "20.50.1.2",
            "verification-code"));

        Assert.Equal(HostnameKind.Subdomain, plan.Kind);
        Assert.Equal("contoso.com", plan.ZoneName);
        Assert.Equal("www", plan.RelativeHost);
        Assert.Contains(plan.Records, r => r.Type == "CNAME" && r.Name == "www" && r.Value == "api.nicehill-1234.westeurope.azurecontainerapps.io.");
        Assert.Contains(plan.Records, r => r.Type == "TXT" && r.Name == "asuid.www" && r.Value == "verification-code");
    }

    [Theory]
    [InlineData("example.com", HostnameKind.Apex)]
    [InlineData("www.example.com", HostnameKind.Subdomain)]
    [InlineData("api.prod.example.com", HostnameKind.Subdomain)]
    public void DetectKind_MatchesExpectation(string hostname, HostnameKind expected)
    {
        Assert.Equal(expected, DnsRecordPlanner.DetectKind(hostname));
    }
}
