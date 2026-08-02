using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Options for <c>.Google(...)</c> on an AuthOps provider builder.
/// </summary>
public sealed class GoogleAuthProviderOptions
{
    /// <summary>
    /// Aspire parameter for the Google Cloud project id. When <see langword="null"/>, AuthOps creates
    /// <c>{providerName}-project-id</c> with a Choice combobox of projects the ADC can access.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ProjectId { get; set; }
}
