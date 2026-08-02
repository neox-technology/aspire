using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Collect, compare, and apply identifier URIs, OAuth2 scopes, and app roles.
/// </summary>
internal static class EntraApiExpositionApplicator
{
    public static IReadOnlyList<AuthDesiredIdentifierUri> CollectDesiredIdentifierUris(AuthAppResource app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var result = new List<AuthDesiredIdentifierUri>();
        foreach (var annotation in app.Annotations.OfType<ApiIdentifierUriAnnotation>())
        {
            if (annotation.UseClientIdTemplate)
            {
                result.Add(new AuthDesiredIdentifierUri
                {
                    Uri = AuthDesiredIdentifierUri.ClientIdTemplateMarker
                });
            }
            else if (!string.IsNullOrWhiteSpace(annotation.LiteralUri))
            {
                result.Add(new AuthDesiredIdentifierUri { Uri = annotation.LiteralUri });
            }
        }

        return result
            .DistinctBy(u => u.Uri, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<AuthDesiredOauth2PermissionScope> CollectDesiredScopes(AuthAppResource app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Annotations.OfType<ExposedApiAnnotation>()
            .Select(a => a.Exposition)
            .OfType<ScopeApiExposition>()
            .Select(s => new AuthDesiredOauth2PermissionScope
            {
                Id = s.PermissionId,
                Value = s.ScopeValue,
                AdminConsentDisplayName = s.AdminConsentDisplayName,
                AdminConsentDescription = s.AdminConsentDescription,
                UserConsentDisplayName = s.UserConsentDisplayName,
                UserConsentDescription = s.UserConsentDescription,
                Type = s.AllowUserConsent ? "User" : "Admin"
            })
            .ToList();
    }

    public static IReadOnlyList<AuthDesiredAppRole> CollectDesiredAppRoles(AuthAppResource app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Annotations.OfType<ExposedApiAnnotation>()
            .Select(a => a.Exposition)
            .OfType<AppRoleApiExposition>()
            .Select(r => new AuthDesiredAppRole
            {
                Id = r.RoleId,
                Value = r.Value,
                DisplayName = r.Value,
                Description = r.Description,
                AllowedMemberTypes = AllowedMemberTypeMapping.ToGraph(r.AllowedMemberType)
            })
            .ToList();
    }

    public static IReadOnlyList<AuthDesiredIdentifierUri> ExtractIdentifierUris(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return (application.IdentifierUris ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => new AuthDesiredIdentifierUri { Uri = u })
            .ToList();
    }

    public static IReadOnlyList<AuthDesiredOauth2PermissionScope> ExtractScopes(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var scopes = application.Api?.Oauth2PermissionScopes;
        if (scopes is null)
        {
            return [];
        }

        return scopes
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => new AuthDesiredOauth2PermissionScope
            {
                Id = s.Id ?? Guid.Empty,
                Value = s.Value!,
                AdminConsentDisplayName = s.AdminConsentDisplayName ?? s.Value!,
                AdminConsentDescription = s.AdminConsentDescription ?? s.Value!,
                UserConsentDisplayName = s.UserConsentDisplayName,
                UserConsentDescription = s.UserConsentDescription,
                Type = string.IsNullOrWhiteSpace(s.Type) ? "Admin" : s.Type
            })
            .ToList();
    }

    public static IReadOnlyList<AuthDesiredAppRole> ExtractAppRoles(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.AppRoles is null)
        {
            return [];
        }

        return application.AppRoles
            .Where(r => !string.IsNullOrWhiteSpace(r.Value))
            .Select(r => new AuthDesiredAppRole
            {
                Id = r.Id ?? Guid.Empty,
                Value = r.Value!,
                DisplayName = r.DisplayName ?? r.Value!,
                Description = r.Description ?? r.Value!,
                AllowedMemberTypes = (r.AllowedMemberTypes ?? ["User"]).ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Resolves deferred <c>api://{ClientId}</c> markers using <paramref name="clientId"/>.
    /// </summary>
    public static IReadOnlyList<AuthDesiredIdentifierUri> ResolveIdentifierUris(
        IReadOnlyList<AuthDesiredIdentifierUri> desired,
        string? clientId)
    {
        ArgumentNullException.ThrowIfNull(desired);

        return desired.Select(u =>
        {
            if (!u.IsClientIdTemplate)
            {
                return u;
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return u;
            }

            return new AuthDesiredIdentifierUri { Uri = $"api://{clientId}" };
        }).ToList();
    }

    /// <summary>
    /// Upsert-merge: keep existing Graph ids when value matches; add missing desired entries.
    /// </summary>
    public static IReadOnlyList<AuthDesiredOauth2PermissionScope> MergeScopesForApply(
        IReadOnlyList<AuthDesiredOauth2PermissionScope> desired,
        IReadOnlyList<AuthDesiredOauth2PermissionScope> existing)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(existing);

        var byValue = existing.ToDictionary(s => s.Value, StringComparer.Ordinal);
        var merged = existing.ToList();

        foreach (var d in desired)
        {
            if (byValue.TryGetValue(d.Value, out var ex))
            {
                var index = merged.FindIndex(s =>
                    string.Equals(s.Value, d.Value, StringComparison.Ordinal));
                merged[index] = d with { Id = ex.Id != Guid.Empty ? ex.Id : d.Id };
            }
            else
            {
                merged.Add(d);
            }
        }

        return merged;
    }

    public static IReadOnlyList<AuthDesiredAppRole> MergeAppRolesForApply(
        IReadOnlyList<AuthDesiredAppRole> desired,
        IReadOnlyList<AuthDesiredAppRole> existing)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(existing);

        var byValue = existing.ToDictionary(r => r.Value, StringComparer.Ordinal);
        var merged = existing.ToList();

        foreach (var d in desired)
        {
            if (byValue.TryGetValue(d.Value, out var ex))
            {
                var index = merged.FindIndex(r =>
                    string.Equals(r.Value, d.Value, StringComparison.Ordinal));
                merged[index] = d with { Id = ex.Id != Guid.Empty ? ex.Id : d.Id };
            }
            else
            {
                merged.Add(d);
            }
        }

        return merged;
    }

    public static IReadOnlyList<string> MergeIdentifierUrisForApply(
        IReadOnlyList<AuthDesiredIdentifierUri> desired,
        IReadOnlyList<AuthDesiredIdentifierUri> existing)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(existing);

        var set = new HashSet<string>(
            existing.Where(u => !u.IsClientIdTemplate).Select(u => u.Uri),
            StringComparer.OrdinalIgnoreCase);

        foreach (var d in desired.Where(u => !u.IsClientIdTemplate))
        {
            set.Add(d.Uri);
        }

        return set.ToList();
    }

    public static bool IdentifierUrisDiffer(
        IReadOnlyList<AuthDesiredIdentifierUri> desired,
        IReadOnlyList<AuthDesiredIdentifierUri> existing,
        string? knownClientId)
    {
        var resolvedDesired = ResolveIdentifierUris(desired, knownClientId)
            .Where(u => !u.IsClientIdTemplate)
            .Select(u => u.Uri)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Create path with only deferred URI: always plan update (applied after AppId known).
        if (desired.Count > 0 && resolvedDesired.Count == 0)
        {
            return true;
        }

        if (resolvedDesired.Count == 0)
        {
            return false;
        }

        var existingSet = existing.Select(u => u.Uri).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !resolvedDesired.IsSubsetOf(existingSet);
    }

    public static bool ScopesDiffer(
        IReadOnlyList<AuthDesiredOauth2PermissionScope> desired,
        IReadOnlyList<AuthDesiredOauth2PermissionScope> existing)
    {
        if (desired.Count == 0)
        {
            return false;
        }

        var byValue = existing.ToDictionary(s => s.Value, StringComparer.Ordinal);
        foreach (var d in desired)
        {
            if (!byValue.TryGetValue(d.Value, out var ex))
            {
                return true;
            }

            if (!string.Equals(ex.AdminConsentDisplayName, d.AdminConsentDisplayName, StringComparison.Ordinal)
                || !string.Equals(ex.AdminConsentDescription, d.AdminConsentDescription, StringComparison.Ordinal)
                || !string.Equals(ex.UserConsentDisplayName ?? "", d.UserConsentDisplayName ?? "", StringComparison.Ordinal)
                || !string.Equals(ex.UserConsentDescription ?? "", d.UserConsentDescription ?? "", StringComparison.Ordinal)
                || !string.Equals(ex.Type, d.Type, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool AppRolesDiffer(
        IReadOnlyList<AuthDesiredAppRole> desired,
        IReadOnlyList<AuthDesiredAppRole> existing)
    {
        if (desired.Count == 0)
        {
            return false;
        }

        var byValue = existing.ToDictionary(r => r.Value, StringComparer.Ordinal);
        foreach (var d in desired)
        {
            if (!byValue.TryGetValue(d.Value, out var ex))
            {
                return true;
            }

            if (!string.Equals(ex.Description, d.Description, StringComparison.Ordinal)
                || !string.Equals(ex.DisplayName, d.DisplayName, StringComparison.Ordinal)
                || !new HashSet<string>(ex.AllowedMemberTypes, StringComparer.OrdinalIgnoreCase)
                    .SetEquals(d.AllowedMemberTypes))
            {
                return true;
            }
        }

        return false;
    }

    public static void ApplyIdentifierUris(Application application, IReadOnlyList<string> uris)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.IdentifierUris = uris.ToList();
    }

    public static void ApplyScopes(Application application, IReadOnlyList<AuthDesiredOauth2PermissionScope> scopes)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Api ??= new ApiApplication();
        application.Api.Oauth2PermissionScopes = scopes.Select(s => new PermissionScope
        {
            Id = s.Id,
            Value = s.Value,
            AdminConsentDisplayName = s.AdminConsentDisplayName,
            AdminConsentDescription = s.AdminConsentDescription,
            UserConsentDisplayName = s.UserConsentDisplayName,
            UserConsentDescription = s.UserConsentDescription,
            Type = s.Type,
            IsEnabled = true
        }).ToList();
    }

    public static void ApplyAppRoles(Application application, IReadOnlyList<AuthDesiredAppRole> roles)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.AppRoles = roles.Select(r => new AppRole
        {
            Id = r.Id,
            Value = r.Value,
            DisplayName = r.DisplayName,
            Description = r.Description,
            AllowedMemberTypes = r.AllowedMemberTypes.ToList(),
            IsEnabled = true
        }).ToList();
    }
}
