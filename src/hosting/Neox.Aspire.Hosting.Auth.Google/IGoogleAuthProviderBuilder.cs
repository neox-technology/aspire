using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Google AuthOps provider builder — register apps with <see cref="AddAppRegistration"/>.
/// </summary>
public interface IGoogleAuthProviderBuilder
{
    /// <summary>
    /// The Google provider resource.
    /// </summary>
    IResourceBuilder<GoogleAuthOpsResource> Resource { get; }

    /// <summary>
    /// Adds an IAM oauth client Auth app under this Google provider.
    /// </summary>
    /// <param name="name">Aspire resource name (slug for pipeline steps and parameters).</param>
    /// <param name="displayName">Required display name on the IAM oauth client.</param>
    IResourceBuilder<AuthAppResource> AddAppRegistration(string name, string displayName);
}
