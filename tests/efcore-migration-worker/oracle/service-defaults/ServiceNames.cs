namespace Neox.Aspire.EntityFrameworkCore.MigrationWorker.Tests.Oracle.ServiceDefaults;

public static class ServiceNames
{
    public const string Prefix = "neox-efmw-ora";

    public static class Infrastructure
    {
        public const string Oracle = $"{Prefix}-infrastructure-oracle";
        public const string Oracle2 = $"{Prefix}-infrastructure-oracle-2";
    }

    public static class Databases
    {
        public const string Application = $"{Prefix}-databases-application";
        public const string Application2 = $"{Prefix}-databases-application-2";
    }

    public static class Workers
    {
        public const string Migration = $"{Prefix}-migration";
        public const string Migration2 = $"{Prefix}-migration-2";
    }
}
