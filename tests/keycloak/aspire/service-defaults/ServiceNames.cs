namespace Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

public static class ServiceNames
{
    public const string Prefix = "neox-keycloak";

    public static class Auth
    {
        public const string Keycloak = $"{AuthPrefix}-keycloak";
        public const string Realm = $"{AuthPrefix}-realm";
        public const string ApiClient = $"{AuthPrefix}-api-client";
        public const string ApiClientId = "neox-api";
        public const string SpaClient = $"{AuthPrefix}-spa-client";
        public const string SpaClientId = "neox-spa";
        private const string AuthPrefix = $"{Prefix}-auth";
    }

    public static class Services
    {
        public const string Api = $"{Prefix}-api";
    }

    public static class Web
    {
        public const string Spa = $"{Prefix}-spa";
    }
}
