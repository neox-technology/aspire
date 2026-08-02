using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public sealed class AuthRedirectUriTests
{
    [Fact]
    public void WithLocalhostRedirectUri_Defaults_IsHttpsLocalhost()
    {
        var (_, web) = CreateApp();

        web.WithLocalhostRedirectUri();

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal("https://localhost", entry.Literal);
        Assert.Null(entry.Parameter);
    }

    [Fact]
    public void WithLocalhostRedirectUri_PortOnly()
    {
        var (_, web) = CreateApp();

        web.WithLocalhostRedirectUri(7281);

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal("https://localhost:7281", entry.Literal);
    }

    [Fact]
    public void WithLocalhostRedirectUri_PathOnly_NormalizesLeadingSlash()
    {
        var (_, web) = CreateApp();

        web.WithLocalhostRedirectUri(path: "signin-oidc");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal("https://localhost/signin-oidc", entry.Literal);
    }

    [Fact]
    public void WithLocalhostRedirectUri_PortAndPath()
    {
        var (_, web) = CreateApp();

        web.WithLocalhostRedirectUri(7281, "/callback");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal("https://localhost:7281/callback", entry.Literal);
    }

    [Fact]
    public void WithRedirectUri_Literal_StoresAbsoluteUri()
    {
        var (_, web) = CreateApp();

        web.WithRedirectUri("https://contoso.example/callback");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Equal("https://contoso.example/callback", entry.Literal);
        Assert.Null(entry.Parameter);
    }

    [Fact]
    public void WithRedirectUri_Parameter_StoresParameterWithoutPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider").Entra();
        var baseUrl = builder.AddParameter("base-url");
        var web = entra.AddAppRegistration("web", "Web");

        web.WithRedirectUri(baseUrl);

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Same(baseUrl.Resource, entry.Parameter);
        Assert.Null(entry.Literal);
        Assert.Null(entry.Path);
    }

    [Fact]
    public void WithRedirectUri_ParameterAndPath_NormalizesPath()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider").Entra();
        var baseUrl = builder.AddParameter("base-url");
        var web = entra.AddAppRegistration("web", "Web");

        web.WithRedirectUri(baseUrl, "signin-oidc");

        var entry = Assert.Single(web.Resource.RedirectUris);
        Assert.Same(baseUrl.Resource, entry.Parameter);
        Assert.Equal("/signin-oidc", entry.Path);
    }

    [Fact]
    public void Entra_TypedRedirects_RecordPlatformAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider").Entra();
        var baseUrl = builder.AddParameter("base-url");
        var web = entra.AddAppRegistration("web", "Web");

        web.WithLocalhostRedirectUri(AuthApplicationType.Web, 7281)
            .WithRedirectUri(AuthApplicationType.Spa, "https://contoso.example/callback")
            .WithRedirectUri(AuthApplicationType.Native, baseUrl, "/app");

        Assert.Equal(3, web.Resource.RedirectUris.Count);
        var typed = Assert.Single(web.Resource.Annotations.OfType<EntraRedirectUrisAnnotation>());
        Assert.Equal(3, typed.Entries.Count);
        Assert.Equal(AuthApplicationType.Web, typed.Entries[0].Type);
        Assert.Equal(AuthApplicationType.Spa, typed.Entries[1].Type);
        Assert.Equal(AuthApplicationType.Native, typed.Entries[2].Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    [InlineData("ftp://contoso.example/callback")]
    public void WithRedirectUri_Literal_RejectsInvalid(string? uri)
    {
        var (_, web) = CreateApp();

        Assert.ThrowsAny<ArgumentException>(() =>
            web.WithRedirectUri(uri!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void WithLocalhostRedirectUri_RejectsInvalidPort(int port)
    {
        var (_, web) = CreateApp();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            web.WithLocalhostRedirectUri(port));
    }

    private static (IDistributedApplicationBuilder Builder, IResourceBuilder<EntraAuthAppRegistrationResource> Web) CreateApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        var entra = builder.AddAuthProvider("provider").Entra();
        var web = entra.AddAppRegistration("web", "Web");
        return (builder, web);
    }
}
