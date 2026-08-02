using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public sealed class EntraRedirectUriApplicatorTests
{
    [Fact]
    public async Task ResolveAsync_ResolvesLiteralAndParameter_SkipsApi()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider").Entra();
        var baseUrl = builder.AddParameter("base-url", "https://contoso.example");
        var app = entra.AddAppRegistration("web", "Web").Resource;

        app.AddRedirectUri(AuthRedirectUri.FromLiteral("https://localhost:7281/signin-oidc"));
        app.AddRedirectUri(AuthRedirectUri.FromParameter(baseUrl.Resource, "/"));
        app.Annotations.Add(new EntraRedirectUrisAnnotation
        {
            Entries =
            {
                new EntraRedirectUriEntry
                {
                    Type = AuthApplicationType.Web,
                    Uri = app.RedirectUris[0]
                },
                new EntraRedirectUriEntry
                {
                    Type = AuthApplicationType.Spa,
                    Uri = app.RedirectUris[1]
                },
                new EntraRedirectUriEntry
                {
                    Type = AuthApplicationType.Api,
                    Uri = AuthRedirectUri.FromLiteral("https://api.example/ignored")
                }
            }
        });

        var resolved = await EntraRedirectUriApplicator.ResolveAsync(app, CancellationToken.None);

        Assert.Equal(2, resolved.Count);
        Assert.Contains(resolved, r => r.Type == AuthApplicationType.Web && r.Uri == "https://localhost:7281/signin-oidc");
        Assert.Contains(resolved, r => r.Type == AuthApplicationType.Spa && r.Uri == "https://contoso.example/");
    }

    [Fact]
    public void Apply_SetsPlatformBuckets()
    {
        var application = new Application();
        var desired = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/web" },
            new() { Type = AuthApplicationType.Spa, Uri = "https://a.example/spa" },
            new() { Type = AuthApplicationType.Native, Uri = "https://a.example/native" }
        };

        EntraRedirectUriApplicator.Apply(application, desired);

        Assert.Equal(["https://a.example/web"], application.Web!.RedirectUris!);
        Assert.Equal(["https://a.example/spa"], application.Spa!.RedirectUris!);
        Assert.Equal(["https://a.example/native"], application.PublicClient!.RedirectUris!);
    }

    [Fact]
    public void Differ_DetectsPerPlatformChanges()
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
    }

    [Fact]
    public void Extract_ReadsGraphBuckets()
    {
        var application = new Application
        {
            Web = new Microsoft.Graph.Models.WebApplication { RedirectUris = ["https://w"] },
            Spa = new SpaApplication { RedirectUris = ["https://s"] },
            PublicClient = new PublicClientApplication { RedirectUris = ["https://n"] }
        };

        var extracted = EntraRedirectUriApplicator.Extract(application);

        Assert.Contains(extracted, r => r.Type == AuthApplicationType.Web && r.Uri == "https://w");
        Assert.Contains(extracted, r => r.Type == AuthApplicationType.Spa && r.Uri == "https://s");
        Assert.Contains(extracted, r => r.Type == AuthApplicationType.Native && r.Uri == "https://n");
    }

    [Fact]
    public void Differ_IgnoresOrderWithinPlatform()
    {
        var desired = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/1" },
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/2" }
        };
        var existing = new List<AuthDesiredRedirectUri>
        {
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/2" },
            new() { Type = AuthApplicationType.Web, Uri = "https://a.example/1" }
        };

        Assert.False(EntraRedirectUriApplicator.Differ(desired, existing));
    }
}
