namespace Neox.Aspire.Hosting.Azure.EntraId.Generators.Internal;

public sealed class PermissionEntryModel {
    public string Description { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public System.Guid Id { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string Value { get; set; } = "";
}
