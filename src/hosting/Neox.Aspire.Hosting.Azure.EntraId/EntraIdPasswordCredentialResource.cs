using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Aspire child resource that creates an Entra application password via Graph
/// <c>addPassword</c> after the parent app registration is ready.
/// Not a provisioning resource; does not emit Bicep <c>passwordCredentials</c>.
/// Implements <see cref="IResourceWithParent{T}"/>; parent readiness uses
/// <see cref="ResourceNotificationService"/> (Aspire forbids <c>WaitFor</c> on a parent).
/// </summary>
public sealed class EntraIdPasswordCredentialResource
    : Resource, IResourceWithParent<AzureEntraIdAppRegistrationResource>, IResourceWithWaitSupport
{
    /// <summary>
    /// Initializes a new <see cref="EntraIdPasswordCredentialResource"/>.
    /// </summary>
    /// <param name="name">Aspire resource name.</param>
    /// <param name="parent">Parent app registration.</param>
    /// <param name="secretParameter">Secret parameter that receives <c>secretText</c>.</param>
    public EntraIdPasswordCredentialResource(
        string name,
        AzureEntraIdAppRegistrationResource parent,
        ParameterResource secretParameter)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(secretParameter);

        Parent = parent;
        SecretParameter = secretParameter;
    }

    /// <summary>
    /// App registration that owns this client secret.
    /// </summary>
    public AzureEntraIdAppRegistrationResource Parent { get; }

    /// <summary>
    /// Secret Aspire parameter filled after Graph <c>addPassword</c> (or reused from config).
    /// </summary>
    public ParameterResource SecretParameter { get; }
}
