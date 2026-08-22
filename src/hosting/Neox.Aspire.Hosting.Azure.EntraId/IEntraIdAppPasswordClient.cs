namespace Neox.Aspire.Hosting.Azure;

/// <summary>
/// Creates an Entra application password via Microsoft Graph REST <c>addPassword</c>.
/// </summary>
internal interface IEntraIdAppPasswordClient
{
    /// <summary>
    /// Calls Graph <c>POST /v1.0/applications/{objectId}/addPassword</c> and returns <c>secretText</c>.
    /// </summary>
    Task<string> AddPasswordAsync(string objectId, string displayName, CancellationToken cancellationToken);
}
