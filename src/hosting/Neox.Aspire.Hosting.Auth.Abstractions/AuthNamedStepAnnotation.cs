using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Marks that an AuthOps named pipeline step has already been registered on a resource.
/// </summary>
internal sealed class AuthNamedStepAnnotation(string stepName) : IResourceAnnotation
{
    public string StepName { get; } = stepName;
}
