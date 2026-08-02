using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Google Cloud AuthOps provider resource created by <c>.Google(...)</c>.
/// </summary>
public sealed class GoogleAuthOpsResource : AuthOpsResourceBase
{
    public GoogleAuthOpsResource(string name, AuthOpsResource authOpsParent)
        : base(name, authOpsParent)
    {
    }

    /// <inheritdoc />
    public override string ProviderSlug => "google";

    /// <summary>
    /// Aspire parameter that supplies the Google Cloud project id (<c>{provider}-project-id</c>).
    /// Bound to Auth apps as <see cref="GoogleAuthAppRegistrationResource.TenantIdParameter"/> (scope id).
    /// </summary>
    public ParameterResource ProjectIdParameter { get; internal set; } = null!;
}
