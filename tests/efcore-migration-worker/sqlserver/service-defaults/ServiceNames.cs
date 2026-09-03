namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.SqlServer.ServiceDefaults;

public static class ServiceNames
{
    public const string Prefix = "neox-efmw-sql";

    public static class Infrastructure
    {
        public const string SqlServer = $"{Prefix}-infrastructure-sqlserver";
    }

    public static class Databases
    {
        public const string Application = $"{Prefix}-databases-application";
        public const string Application2 = $"{Prefix}-databases-application-2";
        public const string ApplicationMulti = $"{Prefix}-databases-application-multi";
        public const string ApplicationMulti2 = $"{Prefix}-databases-application-multi-2";
    }

    public static class Workers
    {
        public const string Migration = $"{Prefix}-migration";
        public const string Migration2 = $"{Prefix}-migration-2";
        public const string MigrationMulti = $"{Prefix}-migration-multi";
    }
}
