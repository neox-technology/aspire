namespace Neox.Aspire.Hosting.Keycloak.Tests.ServiceDefaults;

public static class ServiceNames
{
    public const string Prefix = "neox-keycloak";

    public static class Auth
    {
        public const string Realm = $"{AuthPrefix}-realm";
        public const string ClientId = "neox-spa";
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
