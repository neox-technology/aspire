using System.Collections.Generic;

namespace Neox.Aspire.Hosting.Azure.EntraId.Generators.Internal;

public sealed class PermissionsCatalogModel {
    public string AppId { get; set; } = MicrosoftGraphPermissionsCodeGen.MicrosoftGraphAppId;

    public List<PermissionEntryModel> Application { get; } = new List<PermissionEntryModel>();

    public List<PermissionEntryModel> Delegated { get; } = new List<PermissionEntryModel>();
}
