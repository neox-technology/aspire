using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Runtime-only empty default so Aspire <c>ParameterProcessor</c> does not treat the parameter
/// as missing at startup (unresolved-parameters modal), while AuthOps still treats whitespace as unset.
/// </summary>
internal sealed class AuthDeferredParameterDefault : ParameterDefault
{
    public override string GetDefaultValue() => string.Empty;

    public override void WriteToManifest(ManifestPublishingContext context)
    {
        // Do not publish an empty default into the manifest; CI/deploy must supply Parameters__*.
    }
}
