namespace Neox.Aspire.Hosting.Azure.EntraId.Tests.ServiceDefaults;

public static class ServiceNames {
    public const string Prefix = "neox-entra";

    public static class Auth {
        public const string AccessAsUserScope = $"{AuthPrefix}-aau";
        public const string Api = $"{AuthPrefix}-api";
        public const string ApiCert = $"{AuthPrefix}-api-cert";
        public const string ApiSwagger = $"{AuthPrefix}-api-swagger";
        public const string Spa = $"{AuthPrefix}-spa";
        public const string SpaApp = $"{AuthPrefix}-spa-app";
        private const string AuthPrefix = $"{Prefix}-auth";
    }

    public static class Services {
        public const string Api = $"{ServicesPrefix}-api";
        private const string ServicesPrefix = $"{Prefix}-services";
    }

    public static class Web {
        public const string Spa = $"{WebPrefix}-spa";
        private const string WebPrefix = $"{Prefix}-web";
    }
}