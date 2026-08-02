using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class EntraRedirectUriApplicatorTests
{
    [Fact]
    public async Task ResolveAsync_LiteralAndParameter_SkipsApi()
    {
        var provider = new EntraAuthOpsResource("entra")
        {
            TenantIdParameter = CreateParameter("tenant", "t1")
        };
        var app = new AuthAppResource("web", provider, "Web")
        {
            TenantIdParameter = provider.TenantIdParameter
        };
        provider.RegisterApp(app);

        var baseUrl = CreateParameter("public-base-url", "https://contoso.example/");
        app.AddRedirectUri(AuthRedirectUri.FromLiteral(AuthApplicationType.Web, "https://localhost:7281/signin-oidc"));
        app.AddRedirectUri(AuthRedirectUri.FromParameter(AuthApplicationType.Spa, baseUrl, "/"));
        app.AddRedirectUri(AuthRedirectUri.FromLiteral(AuthApplicationType.Api, "https://api.example/ignored"));

        var resolved = await EntraRedirectUriApplicator.ResolveAsync(app, CancellationToken.None);

        Assert.Equal(2, resolved.Count);
        Assert.Contains(resolved, r => r.Type == AuthApplicationType.Web && r.Uri == "https://localhost:7281/signin-oidc");
        Assert.Contains(resolved, r => r.Type == AuthApplicationType.Spa && r.Uri == "https://contoso.example/");
    }

    [Fact]
    public void Apply_SetsWebSpaAndNativeBuckets()
    {
        var application = new Application();
        var desired = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/web" },
            new() { Type = AuthApplicationType.Spa, Uri = "https://a.example/spa" },
            new() { Type = AuthApplicationType.Native, Uri = "https://a.example/native" }
        };

        EntraRedirectUriApplicator.Apply(application, desired);

        Assert.Equal(["https://a.example/web"], application.Web?.RedirectUris);
        Assert.Equal(["https://a.example/spa"], application.Spa?.RedirectUris);
        Assert.Equal(["https://a.example/native"], application.PublicClient?.RedirectUris);
    }

    [Fact]
    public void Differ_DetectsPlatformChanges()
    {
        var desired = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/web" }
        };
        var existing = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/old" }
        };

        Assert.True(EntraRedirectUriApplicator.Differ(desired, existing));
        Assert.False(EntraRedirectUriApplicator.Differ(desired, desired));
    }

    [Fact]
    public void Extract_ReadsGraphPlatforms()
    {
        var application = new Application
        {
            Web = new Microsoft.Graph.Models.WebApplication { RedirectUris = ["https://w"] },
            Spa = new SpaApplication { RedirectUris = ["https://s"] },
            PublicClient = new PublicClientApplication { RedirectUris = ["https://n"] }
        };

        var extracted = EntraRedirectUriApplicator.Extract(application);

        Assert.Equal(3, extracted.Count);
        Assert.Contains(extracted, r => r.Type == AuthApplicationType.Web && r.Uri == "https://w");
        Assert.Contains(extracted, r => r.Type == AuthApplicationType.Spa && r.Uri == "https://s");
        Assert.Contains(extracted, r => r.Type == AuthApplicationType.Native && r.Uri == "https://n");
    }

    [Fact]
    public void Differ_OrderDoesNotMatterWithinPlatform()
    {
        var a = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/1" },
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/2" }
        };
        var b = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/2" },
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/1" }
        };

        Assert.False(EntraRedirectUriApplicator.Differ(a, b));
    }

    private static ParameterResource CreateParameter(string name, string value) =>
        new(name, _ => value, secret: false);
}
