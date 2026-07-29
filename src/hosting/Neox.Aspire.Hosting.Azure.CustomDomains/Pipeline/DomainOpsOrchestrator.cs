using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Coordinates custom domain verify / guard / provision pipeline actions.
/// Phase 1 provides the surface; planner and provision logic land in later phases.
/// </summary>
public sealed class DomainOpsOrchestrator
{
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;

    public DomainOpsOrchestrator(IProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task VerifyAsync(
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(options);

        _logger.LogInformation(
            "domain-verify registered for resource {Resource} (certificate required: {RequireCertificate}). Full verification lands in a later package revision.",
            targetResource.Name,
            options.RequireCertificateName);

        return Task.CompletedTask;
    }

    public async Task GuardAsync(
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.RequireCertificateName)
        {
            _logger.LogInformation("domain-guard skipped because RequireCertificateName is false.");
            return;
        }

        var value = await certificateName.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Certificate name parameter is empty. Set Parameters__certificateName (or disable RequireCertificateName for bootstrap).");
        }

        _logger.LogInformation("domain-guard passed for certificate parameter.");
    }

    public Task ProvisionAsync(
        IResource targetResource,
        ParameterResource customDomain,
        ParameterResource certificateName,
        AzureCustomDomainOpsOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targetResource);
        ArgumentNullException.ThrowIfNull(customDomain);
        ArgumentNullException.ThrowIfNull(certificateName);
        ArgumentNullException.ThrowIfNull(options);
        _ = _processRunner;

        throw new NotImplementedException(
            "domain-provision is not implemented in the package skeleton. Use a later package version that includes DNS/cert provisioning.");
    }
}
