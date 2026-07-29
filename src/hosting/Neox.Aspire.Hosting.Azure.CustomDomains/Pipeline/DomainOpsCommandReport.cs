using System.Text;
using Neox.Aspire.Hosting.Azure.Pipeline;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Builds Markdown reports for DomainOps dashboard command results.
/// </summary>
internal sealed class DomainOpsCommandReport
{
    private readonly DomainOpsActionKind _kind;
    private readonly string _providerName;
    private readonly List<string> _sections = [];
    private string? _failureDetail;
    private int _bindingCount;

    public DomainOpsCommandReport(DomainOpsActionKind kind, string providerName)
    {
        _kind = kind;
        _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
    }

    public int BindingCount => _bindingCount;

    public void AddVerify(DomainOpsVerifyOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        _bindingCount++;

        var dns = outcome.DnsStatus switch
        {
            DomainOpsDnsCheckStatus.SkippedNoPlanInput =>
                "skipped (no ACA plan input)",
            DomainOpsDnsCheckStatus.PlannedOnly =>
                $"planned {outcome.PlannedRecordCount} record(s) ({outcome.Kind}); observed check not run",
            DomainOpsDnsCheckStatus.Matched =>
                $"{outcome.PlannedRecordCount} record(s) match plan ({outcome.Kind})",
            _ => outcome.DnsStatus.ToString()
        };

        _sections.Add(
            $"""
            ## Binding: `{outcome.TargetResourceName}`

            - **Hostname:** `{outcome.Hostname}`
            - **Certificate guard:** passed
            - **DNS:** {dns}
            """);
    }

    public void AddGuard(string targetResourceName, DomainOpsGuardOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetResourceName);
        ArgumentNullException.ThrowIfNull(outcome);
        _bindingCount++;

        var status = outcome.Skipped
            ? "skipped (`RequireCertificateName` is false)"
            : $"passed (certificate `{outcome.CertificateName}`)";

        _sections.Add(
            $"""
            ## Binding: `{targetResourceName}`

            - **Certificate guard:** {status}
            """);
    }

    public void AddProvision(DomainOpsProvisionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        _bindingCount++;

        _sections.Add(
            $"""
            ## Binding: `{outcome.TargetResourceName}`

            - **Hostname:** `{outcome.Hostname}`
            - **Managed certificate:** `{outcome.CertificateName}`
            - **GitHub variable:** `{outcome.GitHubVariableName}`
            """);
    }

    public void SetFailure(string? targetResourceName, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var scope = string.IsNullOrWhiteSpace(targetResourceName)
            ? "command"
            : $"binding `{targetResourceName}`";
        _failureDetail =
            $"""
            ## Failure

            Failed on {scope}:

            ```
            {exception.Message.Trim()}
            ```
            """;
    }

    public string SummaryMessage
    {
        get
        {
            if (_failureDetail is not null)
            {
                return $"{_kind} failed for provider '{_providerName}'.";
            }

            return $"Completed {_kind} for {_bindingCount} binding(s) on provider '{_providerName}'.";
        }
    }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# DomainOps {_kind}");
        sb.AppendLine();
        sb.AppendLine($"**Provider:** `{_providerName}`");
        sb.AppendLine();

        foreach (var section in _sections)
        {
            sb.AppendLine(section.TrimEnd());
            sb.AppendLine();
        }

        if (_failureDetail is not null)
        {
            sb.AppendLine(_failureDetail.TrimEnd());
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }
}
