using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Follow-up Graph module that sets <c>identifierUris</c> to <c>api://{appId}</c>
/// after the application deployment has produced <c>appId</c>.
/// </summary>
public sealed class AzureEntraIdIdentifierUriResource : AzureProvisioningResource, IResourceWithWaitSupport {

    /// <summary>
    /// Initializes the identifier URI module for <paramref name="api"/>.
    /// </summary>
    public AzureEntraIdIdentifierUriResource(AzureEntraIdAppRegistrationResource api)
        : base(RequireName(api), AzureEntraIdAppRegistrationResource.ConfigureIdentifierUri) {
        Api = api;
    }

    /// <summary>
    /// Application registration whose identifier URI this module sets.
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

        return $"{api.Name}-identifier-uri";
    }
}
