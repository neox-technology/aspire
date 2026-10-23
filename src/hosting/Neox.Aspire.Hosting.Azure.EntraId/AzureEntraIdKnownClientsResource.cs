using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Azure;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Follow-up Graph module that sets <c>api.knownClientApplications</c>
/// after the API registration and its known clients already exist.
/// </summary>
public sealed class AzureEntraIdKnownClientsResource : AzureProvisioningResource, IResourceWithWaitSupport {

    /// <summary>
    /// Initializes the follow-up module for <paramref name="api"/>.
    /// </summary>
    public AzureEntraIdKnownClientsResource(AzureEntraIdAppRegistrationResource api)
        : base(RequireName(api), AzureEntraIdAppRegistrationResource.ConfigureKnownClients) {
        Api = api;
    }

    /// <summary>
    /// API registration whose <c>knownClientApplications</c> this module updates.
    /// </summary>
    internal AzureEntraIdAppRegistrationResource Api { get; }

    /// <summary>
    /// Client registrations added by <c>AsKnownClientOf</c>.
    /// </summary>
    internal List<AzureEntraIdAppRegistrationResource> Clients { get; } = [];

    /// <inheritdoc />
    public override BicepTemplateFile GetBicepTemplateFile(string? directory = null, bool deleteTemporaryFileOnDispose = true) {
        var file = base.GetBicepTemplateFile(directory, deleteTemporaryFileOnDispose);

        AzureEntraIdAppRegistrationResource.WriteGraphExtension(file);

        return file;
    }

    private static string RequireName(AzureEntraIdAppRegistrationResource api) {
        ArgumentNullException.ThrowIfNull(api);

        return $"{api.Name}-known-clients";
    }
}
