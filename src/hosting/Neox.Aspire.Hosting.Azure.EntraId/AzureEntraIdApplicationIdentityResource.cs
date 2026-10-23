using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Follow-up Graph module that reads <c>appId</c> and <c>id</c> from the application
/// created by an earlier deployment, then creates its service principal.
/// The Graph extension does not return those properties from <c>reference()</c>
/// inside the deployment that creates the application.
/// </summary>
public sealed class AzureEntraIdApplicationIdentityResource : AzureProvisioningResource, IResourceWithWaitSupport {

    /// <summary>
    /// Initializes the identity module for <paramref name="api"/>.
    /// </summary>
    public AzureEntraIdApplicationIdentityResource(AzureEntraIdAppRegistrationResource api)
        : base(RequireName(api), AzureEntraIdAppRegistrationResource.ConfigureIdentity) {
        Api = api;
    }

    /// <summary>
    /// Application registration whose client id and object id this module publishes.
    /// </summary>
    internal AzureEntraIdAppRegistrationResource Api { get; }

    /// <inheritdoc />
    public override BicepTemplateFile GetBicepTemplateFile(string? directory = null, bool deleteTemporaryFileOnDispose = true) {
        var file = base.GetBicepTemplateFile(directory, deleteTemporaryFileOnDispose);

        AzureEntraIdAppRegistrationResource.WriteGraphExtension(file);

        return file;
    }

    private static string RequireName(AzureEntraIdAppRegistrationResource api) {
        ArgumentNullException.ThrowIfNull(api);

        return $"{api.Name}-identity";
    }
}
