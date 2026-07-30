namespace Neox.Aspire.Hosting.Azure.Provisioning;

/// <summary>
/// A managed certificate on a Container Apps environment.
/// </summary>
public sealed record AzureManagedCertificateInfo(
    string Name,
    string? SubjectName,
    string Id);
