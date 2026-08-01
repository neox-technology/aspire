using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Enumerates Azure AD / Entra tenants available to the current credential (ARM).
/// </summary>
internal static class EntraTenantEnumerator
{
    /// <summary>
    /// Returns Choice options keyed by tenant id, labeled with display name / domain.
    /// Empty when enumeration fails.
    /// </summary>
    public static async Task<IReadOnlyList<KeyValuePair<string, string>>> TryGetTenantOptionsAsync(
        TokenCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            credential ??= new DefaultAzureCredential();
            var armClient = new ArmClient(credential);
            var options = new List<KeyValuePair<string, string>>();

            await foreach (var tenant in armClient.GetTenants().GetAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var data = tenant.Data;
                var tenantId = data.TenantId?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    continue;
                }

                var displayName = !string.IsNullOrEmpty(data.DisplayName)
                    ? data.DisplayName
                    : !string.IsNullOrEmpty(data.DefaultDomain)
                        ? data.DefaultDomain
                        : "Unknown";

                var description = displayName;
                if (!string.IsNullOrEmpty(data.DefaultDomain) &&
                    !string.Equals(data.DisplayName, data.DefaultDomain, StringComparison.Ordinal))
                {
                    description += $" ({data.DefaultDomain})";
                }

                description += $" — {tenantId}";
                options.Add(KeyValuePair.Create(tenantId, description));
            }

            return options
                .OrderBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
