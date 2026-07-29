namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Options for Azure Container Apps custom domain operations.
/// </summary>
public sealed class AzureCustomDomainOpsOptions
{
    /// <summary>
    /// Aspire resource name of the Container App (used with Azure CLI queries).
    /// </summary>
    public string? ContainerAppResourceName { get; set; }

    /// <summary>
    /// Azure Container Apps environment name. When null, inferred from deploy context / Azure settings.
    /// </summary>
    public string? ContainerAppEnvironmentName { get; set; }

    /// <summary>
    /// Path where the generated OctoDNS config YAML is written (secrets use <c>env/VAR</c> refs only).
    /// </summary>
    public string OctoDnsConfigPath { get; set; } = "octodns.yaml";

    /// <summary>
    /// Directory where generated zone YAML fragments are written before sync.
    /// </summary>
    public string OctoDnsZoneDirectory { get; set; } = "zones";

    /// <summary>
    /// Optional override for the OctoDNS Docker image (defaults to the provider resource image).
    /// </summary>
    public string? OctoDnsDockerImage { get; set; }

    /// <summary>
    /// GitHub Actions repository variable that stores the managed certificate name.
    /// </summary>
    public string CertificateGitHubVariableName { get; set; } = "CERTIFICATE_NAME";

    /// <summary>
    /// When true, <c>domain-verify</c> and <c>domain-guard</c> fail if the certificate parameter is empty.
    /// </summary>
    public bool RequireCertificateName { get; set; } = true;

    /// <summary>
    /// Managed certificate name to create/bind when provisioning. Defaults to a sanitized hostname.
    /// </summary>
    public string? ManagedCertificateName { get; set; }

    /// <summary>
    /// Maximum time to wait for DNS propagation before hostname bind.
    /// </summary>
    public TimeSpan DnsPropagationTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Polling interval while waiting for DNS or certificate readiness.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(15);
}
