using System.Text.Json;
using Neox.Aspire.Hosting.Azure.Processes;

namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Azure CLI-backed <see cref="IAzureContainerAppReader"/>.
/// </summary>
public sealed class AzureCliContainerAppReader : IAzureContainerAppReader
{
    private readonly IProcessRunner _processRunner;

    public AzureCliContainerAppReader(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public async Task<AzureContainerAppTargets> GetTargetsAsync(
        string containerAppName,
        string? resourceGroup,
        string? environmentName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerAppName);

        resourceGroup ??= Environment.GetEnvironmentVariable("Azure__ResourceGroup")
            ?? Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP")
            ?? throw new InvalidOperationException("Resource group is required (options or Azure__ResourceGroup).");

        var showArgs = new List<string>
        {
            "containerapp", "show",
            "-n", containerAppName,
            "-g", resourceGroup,
            "-o", "json"
        };

        var show = await RunAzAsync(showArgs, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(show.StandardOutput);
        var root = doc.RootElement;

        var fqdn = root.GetProperty("properties").GetProperty("configuration").GetProperty("ingress").GetProperty("fqdn").GetString()
            ?? throw new InvalidOperationException("Container App ingress FQDN was not found.");
        var asuid = root.GetProperty("properties").GetProperty("customDomainVerificationId").GetString()
            ?? throw new InvalidOperationException("Container App customDomainVerificationId was not found.");

        environmentName ??= root.GetProperty("properties").GetProperty("environmentId").GetString()?.Split('/').LastOrDefault()
            ?? throw new InvalidOperationException("Container App environment name could not be resolved.");

        var envShow = await RunAzAsync(
            [
                "containerapp", "env", "show",
                "-n", environmentName,
                "-g", resourceGroup,
                "-o", "json"
            ],
            cancellationToken).ConfigureAwait(false);

        using var envDoc = JsonDocument.Parse(envShow.StandardOutput);
        var staticIp = envDoc.RootElement.GetProperty("properties").GetProperty("staticIp").GetString()
            ?? throw new InvalidOperationException("Container Apps environment staticIp was not found.");

        return new AzureContainerAppTargets(containerAppName, resourceGroup, environmentName, fqdn, staticIp, asuid);
    }

    private async Task<ProcessResult> RunAzAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var result = await _processRunner.RunAsync("az", args, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"az {(args.Count > 0 ? args[0] : "")} failed ({result.ExitCode}): {result.StandardError}");
        }

        return result;
    }
}
