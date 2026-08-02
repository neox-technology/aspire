using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Marks an Entra Auth app for workload client-secret bind and UI create (Graph <c>addPassword</c>),
/// and holds the dedicated <see cref="EntraClientSecretResource"/> child.
/// </summary>
public sealed class ClientSecretAnnotation(EntraClientSecretResource secretResource) : IResourceAnnotation
{
    public EntraClientSecretResource SecretResource { get; } =
        secretResource ?? throw new ArgumentNullException(nameof(secretResource));
}
