using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Marks an <see cref="ApiExposition"/> child owned by an <see cref="EntraAuthAppRegistrationResource"/>.
/// </summary>
public sealed class ExposedApiAnnotation(ApiExposition exposition) : IResourceAnnotation
{
    public ApiExposition Exposition { get; } = exposition
        ?? throw new ArgumentNullException(nameof(exposition));
}
