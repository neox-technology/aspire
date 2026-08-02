namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// OAuth application platform type for Entra Graph redirect URI buckets
/// (<c>web</c>, <c>spa</c>, <c>publicClient</c>). <see cref="Api"/> has no Graph redirect bucket.
/// </summary>
public enum AuthApplicationType
{
    Web,
    Spa,
    Api,
    Native
}
