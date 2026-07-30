namespace Neox.Aspire.Hosting.Azure.Processes;

/// <summary>
/// Runs external processes used by DomainOps (Docker for OctoDNS sync in V1).
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null);
}
