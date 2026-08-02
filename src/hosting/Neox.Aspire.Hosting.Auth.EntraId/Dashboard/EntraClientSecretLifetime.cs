namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Entra portal-style client secret lifetime presets (months).
/// </summary>
internal static class EntraClientSecretLifetime
{
    public const string SixMonths = "6";
    public const string TwelveMonths = "12";
    public const string TwentyFourMonths = "24";
    public const string DefaultKey = TwelveMonths;

    public static IReadOnlyList<KeyValuePair<string, string>> ChoiceOptions { get; } =
    [
        KeyValuePair.Create(SixMonths, "6 months"),
        KeyValuePair.Create(TwelveMonths, "12 months"),
        KeyValuePair.Create(TwentyFourMonths, "24 months")
    ];

    public static DateTimeOffset ResolveEndDateTime(string? lifetimeKey, DateTimeOffset utcNow)
    {
        var months = lifetimeKey switch
        {
            SixMonths => 6,
            TwentyFourMonths => 24,
            _ => 12
        };

        return utcNow.AddMonths(months);
    }
}
