using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Pipeline;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Registers dashboard resource commands on DomainOps provider resources.
/// </summary>
internal static class DomainOpsProviderCommandExtensions
{
    public static void EnsureProviderCommands<TProvider>(IResourceBuilder<TProvider> provider)
        where TProvider : DomainOpsProviderResource
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.Resource.Annotations.OfType<DomainOpsProviderCommandsAnnotation>().Any())
        {
            return;
        }

        provider.WithAnnotation(new DomainOpsProviderCommandsAnnotation());

        var providerResource = provider.Resource;

        provider.WithCommand(
            AzureCustomDomainOpsExtensions.DomainVerifyStepName,
            "Verify",
            context => ExecuteAsync(context, providerResource, DomainOpsActionKind.Verify),
            new CommandOptions
            {
                Description = "Verify DNS and certificate parameter consistency for bindings using this provider.",
                IconName = "Checkmark",
                IconVariant = IconVariant.Filled
            });

        provider.WithCommand(
            AzureCustomDomainOpsExtensions.DomainGuardStepName,
            "Guard",
            context => ExecuteAsync(context, providerResource, DomainOpsActionKind.Guard),
            new CommandOptions
            {
                Description = "Fail when a certificate name is required and empty (steady-state) for bindings using this provider.",
                IconName = "Shield",
                IconVariant = IconVariant.Filled
            });

        provider.WithCommand(
            AzureCustomDomainOpsExtensions.DomainProvisionStepName,
            "Deploy",
            context => ExecuteAsync(context, providerResource, DomainOpsActionKind.Provision),
            new CommandOptions
            {
                Description = "Provision DNS via OctoDNS, bind ACA managed certificate, and update the GitHub variable (domain-provision).",
                IconName = "CloudArrowUp",
                IconVariant = IconVariant.Filled,
                ConfirmationMessage =
                    "Provision DNS, bind managed certificates, and update GitHub variables for all bindings using this provider?"
            });
    }

    private static async Task<ExecuteCommandResult> ExecuteAsync(
        ExecuteCommandContext context,
        DomainOpsProviderResource provider,
        DomainOpsActionKind kind)
    {
        try
        {
            var model = context.ServiceProvider.GetRequiredService<DistributedApplicationModel>();
            var bindings = DomainOpsProviderCommandBindings.FindBindings(model, provider);
            if (bindings.Count == 0)
            {
                return CommandResults.Failure(
                    $"No custom domain bindings reference DomainOps provider '{provider.Name}'.");
            }

            var loggerFactory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger($"domain-ops-command-{kind.ToString().ToLowerInvariant()}");
            var runner = context.ServiceProvider.GetService<IProcessRunner>() ?? new ProcessRunner();
            var orchestrator = new DomainOpsOrchestrator(runner, logger);

            foreach (var (target, annotation) in bindings)
            {
                await DomainOpsParameterPrompt.EnsureReadyAsync(
                        context.ServiceProvider,
                        DomainOpsParameterPrompt.CollectRequired(
                            kind,
                            annotation.CustomDomain.Resource,
                            annotation.CertificateName.Resource,
                            provider,
                            annotation.Options),
                        context.CancellationToken)
                    .ConfigureAwait(false);

                switch (kind)
                {
                    case DomainOpsActionKind.Verify:
                        await orchestrator.VerifyAsync(
                                target,
                                annotation.CustomDomain.Resource,
                                annotation.CertificateName.Resource,
                                annotation.Options,
                                context.CancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case DomainOpsActionKind.Guard:
                        await orchestrator.GuardAsync(
                                annotation.CertificateName.Resource,
                                annotation.Options,
                                context.CancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case DomainOpsActionKind.Provision:
                        await orchestrator.ProvisionAsync(
                                target,
                                annotation.CustomDomain.Resource,
                                annotation.CertificateName.Resource,
                                provider,
                                annotation.Options,
                                context.CancellationToken)
                            .ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown DomainOps command kind '{kind}'.");
                }
            }

            return CommandResults.Success(
                $"Completed {kind} for {bindings.Count} binding(s) on provider '{provider.Name}'.");
        }
        catch (OperationCanceledException)
        {
            return CommandResults.Canceled();
        }
        catch (Exception ex)
        {
            return CommandResults.Failure(ex);
        }
    }
}
