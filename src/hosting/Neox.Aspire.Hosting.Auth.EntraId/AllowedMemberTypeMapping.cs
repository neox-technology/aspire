namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Maps <see cref="AllowedMemberType"/> to Graph <c>allowedMemberTypes</c>.
/// </summary>
public static class AllowedMemberTypeMapping
{
    public static IReadOnlyList<string> ToGraph(AllowedMemberType type) =>
        type switch
        {
            AllowedMemberType.UsersAndGroups => ["User"],
            AllowedMemberType.Applications => ["Application"],
            AllowedMemberType.Both => ["User", "Application"],
            _ => ["User"]
        };

    public static AllowedMemberType FromGraph(IEnumerable<string>? types)
    {
        if (types is null)
        {
            return AllowedMemberType.UsersAndGroups;
        }

        var set = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);
        var hasUser = set.Contains("User");
        var hasApp = set.Contains("Application");
        if (hasUser && hasApp)
        {
            return AllowedMemberType.Both;
        }

        if (hasApp)
        {
            return AllowedMemberType.Applications;
        }

        return AllowedMemberType.UsersAndGroups;
    }
}
