using Neox.Aspire.Hosting.Azure.Dns;

namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// Planned custom-domain binding for a compute resource (no ARM side effects).
/// </summary>
public sealed record DomainBindingPlan(
    string TargetResourceName,
    string Hostname,
    string CertificateName,
    string ValidationMethod,
    HostnameKind Kind);
