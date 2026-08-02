namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Google IAM oauth client Auth app under a <see cref="GoogleAuthOpsResource"/>.
/// </summary>
public sealed class GoogleAuthAppRegistrationResource : AuthAppRegistrationResource
{
    public GoogleAuthAppRegistrationResource(string name, GoogleAuthOpsResource provider, string displayName)
        : base(name, provider, displayName)
    {
        GoogleProvider = provider;
    }

    /// <summary>
    /// Parent Google Auth provider.
    /// </summary>
    public GoogleAuthOpsResource GoogleProvider { get; }

    /// <summary>
    /// Default env prefix for a single app: <c>AUTH_GOOGLE</c>; for multiple apps: <c>AUTH_GOOGLE_{APP}</c>.
    /// </summary>
    public string DefaultEnvPrefix
    {
        get
        {
            var providerToken = Provider.ProviderEnvToken;
            if (Provider.Apps.Count <= 1)
            {
                return $"AUTH_{providerToken}";
            }

            return $"AUTH_{providerToken}_{AuthOpsResourceBase.SanitizeEnvToken(Name)}";
        }
    }
}
