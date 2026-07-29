using Neox.Aspire.Hosting.Azure.Dns;

namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// How far DNS verification progressed for a binding.
/// </summary>
public enum DomainOpsDnsCheckStatus
{
    /// <summary>No ACA plan input (env or explicit); DNS drift was not evaluated.</summary>
    SkippedNoPlanInput,

    /// <summary>Records were planned but no observed records were supplied.</summary>
    PlannedOnly,

    /// <summary>Observed DNS records matched the plan.</summary>
    Matched
}

/// <summary>
/// Outcome of <see cref="DomainOpsOrchestrator.VerifyAsync"/>.
/// </summary>
public sealed record DomainOpsVerifyOutcome(
    string TargetResourceName,
    string Hostname,
    DomainOpsDnsCheckStatus DnsStatus,
    HostnameKind? Kind = null,
    int? PlannedRecordCount = null);

/// <summary>
/// Outcome of <see cref="DomainOpsOrchestrator.GuardAsync"/>.
/// </summary>
public sealed record DomainOpsGuardOutcome(
    bool Skipped,
    string? CertificateName = null);

/// <summary>
/// Outcome of <see cref="DomainOpsOrchestrator.ProvisionAsync"/>.
/// </summary>
public sealed record DomainOpsProvisionOutcome(
    string TargetResourceName,
    string Hostname,
    string CertificateName,
    string GitHubVariableName);
