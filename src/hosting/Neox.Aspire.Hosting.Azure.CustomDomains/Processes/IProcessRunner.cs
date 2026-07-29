namespace Neox.Aspire.Hosting.Azure.Processes;

/// <summary>
/// Result of an external process invocation.
/// </summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Abstraction over process execution for Azure CLI, OctoDNS, and GitHub CLI.
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
