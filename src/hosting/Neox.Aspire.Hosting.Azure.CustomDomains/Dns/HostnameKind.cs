namespace Neox.Aspire.Hosting.Azure.Dns;

/// <summary>
/// Whether the custom hostname is an apex (root) or a subdomain.
/// </summary>
public enum HostnameKind
{
    Apex,
    Subdomain
}
