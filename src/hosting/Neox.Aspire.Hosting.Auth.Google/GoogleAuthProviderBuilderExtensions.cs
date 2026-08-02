#pragma warning disable ASPIREINTERACTION001

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Google Cloud extensions for <see cref="IAuthProviderBuilder"/>.
/// </summary>
public static class GoogleAuthProviderBuilderExtensions
{
    /// <summary>
    /// Configures this provider as Google Cloud IAM oauthClients, creating a <see cref="GoogleAuthOpsResource"/>.
    /// </summary>
    public static IGoogleAuthProviderBuilder Google(
        this IAuthProviderBuilder builder,
        Action<GoogleAuthProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new GoogleAuthProviderOptions();
        configure?.Invoke(options);

        var applicationBuilder = builder.ApplicationBuilder;
        var name = builder.Name;

        var authOps = AuthOpsExtensions.EnsureAuthOpsResource(applicationBuilder);

        var resource = new GoogleAuthOpsResource(name, authOps.Resource)
        {
            // ProjectId is not part of the Google OIDC authority URL.
            AuthorityExpression = static _ =>
                ReferenceExpression.Create($"https://accounts.google.com")
        };

        var projectParam = options.ProjectId
            ?? AuthOpsExtensions.GetOrAddParameter(
                applicationBuilder,
                $"{name}-project-id",
                defaultValue: null,
                secret: false);

        ConfigureProjectChoiceInput(projectParam, name);
        resource.ProjectIdParameter = projectParam.Resource;

        var providerBuilder = applicationBuilder.AddResource(resource)
            .ExcludeFromManifest()
            .WithParentRelationship(authOps)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "AuthProvider",
                State = KnownResourceStates.Running,
                Properties = []
            });

        projectParam.WithParentRelationship(providerBuilder);

        GoogleAuthOpsExtensions.EnsurePrereqGoogleStep(applicationBuilder, providerBuilder);

        return new GoogleAuthProviderBuilder(applicationBuilder, providerBuilder);
    }

    private static void ConfigureProjectChoiceInput(
        IResourceBuilder<ParameterResource> projectParam,
        string providerResourceName)
    {
        if (projectParam.Resource.Annotations.OfType<InputGeneratorAnnotation>().Any())
        {
            return;
        }

        projectParam.WithCustomInput(parameter => new InteractionInput
        {
            Name = parameter.Name,
            InputType = InputType.Choice,
            Label = GoogleProjectParameterPrompt.FormatLabel(providerResourceName),
            Description = GoogleProjectParameterPrompt.FormatDescription(
                hasProjects: true,
                providerResourceName,
                parameter.Name),
            Required = true,
            AllowCustomChoice = true,
            Options = [],
            DynamicLoading = new InputLoadOptions
            {
                LoadCallback = async context =>
                {
                    var projects = await GoogleProjectEnumerator.TryGetProjectOptionsAsync(
                            cancellationToken: context.CancellationToken)
                        .ConfigureAwait(false);
                    if (projects.Count > 0)
                    {
                        context.Input.Options = projects;
                    }
                }
            }
        });
    }
}
