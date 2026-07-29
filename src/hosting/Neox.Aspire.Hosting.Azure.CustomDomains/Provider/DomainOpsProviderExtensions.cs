using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Extension methods that register OctoDNS DomainOps provider resources.
/// </summary>
public static class DomainOpsProviderExtensions
{
    /// <summary>
    /// Starts configuring a DomainOps DNS provider resource (select Cloudflare or OVH next).
    /// </summary>
    public static IDomainOpsProviderBuilder AddDomainOpsProvider(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DomainOpsProviderBuilder(builder, name);
    }

    private sealed class DomainOpsProviderBuilder(
        IDistributedApplicationBuilder applicationBuilder,
        string name) : IDomainOpsProviderBuilder
    {
        public IResourceBuilder<CloudflareDomainOpsProviderResource> Cloudflare(
            CloudflareDomainOpsProviderOptions? options = null)
        {
            options ??= new CloudflareDomainOpsProviderOptions();
            var resource = new CloudflareDomainOpsProviderResource(name);

            BindSecret(resource, "token", options.Token, secret: true);
            if (options.AccountId is not null)
            {
                resource.BindAuthParameter("account_id", options.AccountId.Resource);
            }

            return AddProviderResource(resource);
        }

        public IResourceBuilder<OvhDomainOpsProviderResource> Ovh(
            OvhDomainOpsProviderOptions? options = null)
        {
            options ??= new OvhDomainOpsProviderOptions();
            var resource = new OvhDomainOpsProviderResource(name);

            resource.SetLiteral("endpoint", string.IsNullOrWhiteSpace(options.Endpoint) ? "ovh-eu" : options.Endpoint);
            BindSecret(resource, "application_key", options.ApplicationKey, secret: true);
            BindSecret(resource, "application_secret", options.ApplicationSecret, secret: true);
            BindSecret(resource, "consumer_key", options.ConsumerKey, secret: true);

            return AddProviderResource(resource);
        }

        private void BindSecret(
            DomainOpsProviderResource resource,
            string yamlPropertyName,
            IResourceBuilder<ParameterResource>? existing,
            bool secret)
        {
            if (existing is not null)
            {
                resource.BindAuthParameter(yamlPropertyName, existing.Resource);
                return;
            }

            var parameterName = resource.GetParameterName(yamlPropertyName);
            var parameter = applicationBuilder.AddParameter(parameterName, secret: secret);
            resource.BindAuthParameter(yamlPropertyName, parameter.Resource);
        }

        private IResourceBuilder<T> AddProviderResource<T>(T resource)
            where T : DomainOpsProviderResource
        {
            return applicationBuilder.AddResource(resource)
                .ExcludeFromManifest()
                .WithInitialState(new CustomResourceSnapshot
                {
                    ResourceType = "DomainOpsProvider",
                    State = KnownResourceStates.Running,
                    Properties = []
                });
        }
    }
}
