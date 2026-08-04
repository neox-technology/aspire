#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class GoogleAuthProviderApiTests
{
    [Fact]
    public void Google_AutoCreatesProjectParameter_WithChoiceInput()
    {
        var builder = DistributedApplication.CreateBuilder();
        var google = builder.AddAuthProvider("provider-google").Google();

        var project = Assert.Single(
            builder.Resources.OfType<ParameterResource>(),
            p => p.Name == "provider-google-project-id");
        Assert.Same(project, google.Resource.Resource.ProjectIdParameter);
        Assert.Contains(project.Annotations.OfType<InputGeneratorAnnotation>(), _ => true);
        Assert.Equal("google", google.Resource.Resource.ProviderSlug);
    }

    [Fact]
    public void Google_WithAuth_UsesGooglePrefix()
    {
        var builder = DistributedApplication.CreateBuilder();
        var google = builder.AddAuthProvider("provider-google")
            .Google(o => o.ProjectId = builder.AddParameter("p", "proj"));
        var web = google.AddAppRegistration("web", "Web");

        Assert.Equal("AUTH_GOOGLE", web.Resource.DefaultEnvPrefix);
    }

    [Fact]
    public void Google_WithAuth_WaitsForAuthApp_Deduped()
    {
        var builder = DistributedApplication.CreateBuilder();
        var google = builder.AddAuthProvider("provider-google")
            .Google(o => o.ProjectId = builder.AddParameter("p", "proj"));
        var web = google.AddAppRegistration("web", "Web");

        var api = builder.AddContainer("api", "mcr.microsoft.com/dotnet/runtime", "10.0");
        api.WithAuth(web);
        api.WithAuth(web, env => env.IncludeClientSecret = true);

        var wait = Assert.Single(api.Resource.Annotations.OfType<WaitAnnotation>());
        Assert.Same(web.Resource, wait.Resource);
        Assert.Equal(WaitType.WaitUntilHealthy, wait.WaitType);
    }

    [Fact]
    public async Task Google_RegistersPipelineFanIn()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"]
        });

        var project = builder.AddParameter("provider-google-project-id", "my-project");
        var google = builder.AddAuthProvider("provider-google")
            .Google(o => o.ProjectId = project);
        google.AddAppRegistration("web", "Test Web")
            .WithLocalhostRedirectUri(7281, "/signin-oidc");

        var steps = await CollectAuthOpsStepsAsync(builder);

        Assert.Single(builder.Resources.OfType<AuthOpsResource>());

        var prereqGoogle = Assert.Single(steps, s => s.Name == "prereq-provider-google-auth");
        Assert.Contains("prereq-providers-auth", prereqGoogle.RequiredBySteps);

        Assert.Single(steps, s => s.Name == "prereq-providers-auth");
        Assert.Single(steps, s => s.Name == "prereq-web-auth");
        Assert.Single(steps, s => s.Name == "plan-web-auth");

        var provision = Assert.Single(steps, s => s.Name == "provision-web-auth");
        Assert.Contains("deploy-auth", provision.RequiredBySteps);
        Assert.Single(steps, s => s.Name == "deploy-auth");
    }

    [Fact]
    public void PromptLabels_IdentifyGoogleProviderAndApp()
    {
        Assert.Equal("Google project — provider-google", GoogleProjectParameterPrompt.FormatLabel("provider-google"));
        Assert.Equal(
            "Google oauth client — web (MyApp)",
            GoogleOauthClientParameterPrompt.FormatLabel("web", "MyApp"));
        Assert.True(GoogleOauthClientParameterPrompt.IsCreateSentinel("__create__"));
        Assert.False(GoogleOauthClientParameterPrompt.IsCreateSentinel(null));
        Assert.False(GoogleOauthClientParameterPrompt.IsCreateSentinel("real-id"));
        Assert.Contains(
            "does not create",
            GoogleOauthClientParameterPrompt.FormatDescription(
                hasClients: false,
                "provider-google",
                "web",
                "provider-google-web-client-id"),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<List<PipelineStep>> CollectAuthOpsStepsAsync(IDistributedApplicationBuilder builder)
    {
        var model = new DistributedApplicationModel([.. builder.Resources]);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var services = new ServiceCollection().BuildServiceProvider();
        var pipelineContext = new PipelineContext(
            model,
            executionContext,
            services,
            NullLogger.Instance,
            CancellationToken.None);

        var steps = new List<PipelineStep>();
        foreach (var resource in builder.Resources.Where(static r =>
                     r is AuthOpsResource or AuthOpsResourceBase or GoogleAuthAppRegistrationResource))
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineStepAnnotation>())
            {
                var context = new PipelineStepFactoryContext
                {
                    Resource = resource,
                    PipelineContext = pipelineContext
                };
                var created = await annotation.CreateStepsAsync(context);
                steps.AddRange(created);
            }
        }

        return steps;
    }
}
