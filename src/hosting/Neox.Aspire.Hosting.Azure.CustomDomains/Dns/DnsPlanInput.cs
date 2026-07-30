namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Inputs required to plan ACA custom domain DNS records.
/// </summary>
public sealed record DnsPlanInput(
    string CustomHostname,
    string ContainerAppFqdn,
    string EnvironmentStaticIp,
    string CustomDomainVerificationId,
    int Ttl = 0);
