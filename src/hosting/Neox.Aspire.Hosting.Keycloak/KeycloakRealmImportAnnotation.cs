using Aspire.Hosting.ApplicationModel;

namespace Neox.Aspire.Hosting.Keycloak;

internal sealed class KeycloakRealmImportAnnotation(string importDirectory) : IResourceAnnotation
{
    public string ImportDirectory { get; } = importDirectory;
}
