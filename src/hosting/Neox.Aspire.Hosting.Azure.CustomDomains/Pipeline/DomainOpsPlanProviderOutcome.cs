namespace Neox.Aspire.Hosting.Azure.Pipeline;

/// <summary>
/// Outcome of planning an OctoDNS provider config file.
/// </summary>
public sealed record DomainOpsPlanProviderOutcome(
    string ProviderSlug,
    string ConfigPath,
    IReadOnlyList<string> ZoneNames);
