using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Resolves compute bindings that reference a given DomainOps provider.
/// </summary>
internal static class DomainOpsProviderCommandBindings
{
    public static IReadOnlyList<(IResource Target, AzureCustomDomainOpsAnnotation Annotation)> FindBindings(
        DistributedApplicationModel model,
        DomainOpsProviderResource provider)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(provider);

        var bindings = new List<(IResource, AzureCustomDomainOpsAnnotation)>();
        foreach (var resource in model.Resources)
        {
            foreach (var annotation in resource.Annotations.OfType<AzureCustomDomainOpsAnnotation>())
            {
                if (ReferenceEquals(annotation.Provider, provider))
                {
                    bindings.Add((resource, annotation));
                }
            }
        }

        return bindings;
    }
}
