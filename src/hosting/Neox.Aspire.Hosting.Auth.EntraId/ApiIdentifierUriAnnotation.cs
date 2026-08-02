using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Desired Application ID URI (<c>identifierUris</c>) from <c>WithApiExposition</c>.
/// </summary>
public sealed class ApiIdentifierUriAnnotation : IResourceAnnotation
{
    public ApiIdentifierUriAnnotation(string? literalUri, bool useClientIdTemplate)
    {
        if (useClientIdTemplate)
        {
            if (!string.IsNullOrWhiteSpace(literalUri))
            {
                throw new ArgumentException(
                    "Literal URI must be null when using the api://{ClientId} template.",
                    nameof(literalUri));
            }

            UseClientIdTemplate = true;
            LiteralUri = null;
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(literalUri);
            LiteralUri = literalUri;
            UseClientIdTemplate = false;
        }
    }

    /// <summary>Literal Application ID URI when not using the ClientId template.</summary>
    public string? LiteralUri { get; }

    /// <summary>
    /// When <see langword="true"/>, resolve to <c>api://{ClientId}</c> after the app ClientId is known.
    /// </summary>
    public bool UseClientIdTemplate { get; }
}
