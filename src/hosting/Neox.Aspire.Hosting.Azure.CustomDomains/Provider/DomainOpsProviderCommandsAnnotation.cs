using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Marker that Verify / Guard / Deploy dashboard commands were registered on a DomainOps provider.
/// </summary>
internal sealed class DomainOpsProviderCommandsAnnotation : IResourceAnnotation;
