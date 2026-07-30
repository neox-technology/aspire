namespace Neox.Aspire.Hosting.Azure.Processes;

/// <summary>
/// Result of an external process invocation.
/// </summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// <see langword="true"/> when the process exited with code 0.
    /// </summary>
    public bool Succeeded => ExitCode == 0;
}
