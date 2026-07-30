namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// One hostname binding participating in a zone plan.
/// </summary>
public sealed record ZoneBindingInput(string Hostname, string ContainerAppResourceName);
