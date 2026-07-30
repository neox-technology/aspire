namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Outcome of planning a DNS zone YAML.
/// </summary>
public sealed record DomainOpsPlanZoneOutcome(
    string ZoneName,
    int BindingCount);
