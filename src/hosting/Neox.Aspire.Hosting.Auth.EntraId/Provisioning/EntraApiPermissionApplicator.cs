using Aspire.Hosting.ApplicationModel;
using Microsoft.Graph.Models;

namespace Neox.Aspire.Hosting.Auth;

/// <summary>
/// Collect, compare, and apply Graph <c>requiredResourceAccess</c> from <c>WithApiPermission</c>.
/// </summary>
internal static class EntraApiPermissionApplicator
{
    /// <summary>
    /// Builds desired required resource access. Returns entries whose exposer ClientId is known;
    /// skips permissions whose exposer ClientId cannot be resolved yet (create path of exposer).
    /// </summary>
    public static IReadOnlyList<AuthDesiredRequiredResourceAccess> CollectDesired(
        AuthAppResource app,
        Func<AuthAppResource, string?> resolveExposerClientId)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(resolveExposerClientId);

        var result = new List<AuthDesiredRequiredResourceAccess>();
        foreach (var annotation in app.Annotations.OfType<ApiPermissionAnnotation>())
        {
            var exposition = annotation.Exposition;
            var resourceAppId = resolveExposerClientId(exposition.Owner);
            if (string.IsNullOrWhiteSpace(resourceAppId))
            {
                // Exposer not provisioned yet — plan will still flag update when any permission is declared
                // and resourceAppId is unknown at adopt-compare time; create path applies after DependsOn.
                continue;
            }

            switch (exposition)
            {
                case ScopeApiExposition scope:
                    result.Add(new AuthDesiredRequiredResourceAccess
                    {
                        ResourceAppId = resourceAppId,
                        PermissionId = scope.PermissionId,
                        Type = "Scope"
                    });
                    break;
                case AppRoleApiExposition role:
                    result.Add(new AuthDesiredRequiredResourceAccess
                    {
                        ResourceAppId = resourceAppId,
                        PermissionId = role.RoleId,
                        Type = "Role"
                    });
                    break;
            }
        }

        return result;
    }

    public static bool HasDeclaredPermissions(AuthAppResource app) =>
        app.Annotations.OfType<ApiPermissionAnnotation>().Any();

    public static IReadOnlyList<AuthDesiredRequiredResourceAccess> Extract(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.RequiredResourceAccess is null)
        {
            return [];
        }

        var result = new List<AuthDesiredRequiredResourceAccess>();
        foreach (var resource in application.RequiredResourceAccess)
        {
            if (string.IsNullOrWhiteSpace(resource.ResourceAppId) || resource.ResourceAccess is null)
            {
                continue;
            }

            foreach (var access in resource.ResourceAccess)
            {
                if (access.Id is null || string.IsNullOrWhiteSpace(access.Type))
                {
                    continue;
                }

                result.Add(new AuthDesiredRequiredResourceAccess
                {
                    ResourceAppId = resource.ResourceAppId,
                    PermissionId = access.Id.Value,
                    Type = access.Type
                });
            }
        }

        return result;
    }

    /// <summary>
    /// True when any desired entry is missing from existing (upsert semantics).
    /// </summary>
    public static bool Differ(
        IReadOnlyList<AuthDesiredRequiredResourceAccess> desired,
        IReadOnlyList<AuthDesiredRequiredResourceAccess> existing)
    {
        if (desired.Count == 0)
        {
            return false;
        }

        var existingSet = existing
            .Select(Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return desired.Any(d => !existingSet.Contains(Key(d)));
    }

    /// <summary>
    /// Merges desired into existing requiredResourceAccess without removing other entries.
    /// Remaps permission ids when exposer scopes/roles were matched by value on the exposer app.
    /// </summary>
    public static IReadOnlyList<RequiredResourceAccess> MergeForApply(
        IReadOnlyList<AuthDesiredRequiredResourceAccess> desired,
        IReadOnlyList<AuthDesiredRequiredResourceAccess> existing)
    {
        ArgumentNullException.ThrowIfNull(desired);
        ArgumentNullException.ThrowIfNull(existing);

        var byResource = new Dictionary<string, Dictionary<(Guid Id, string Type), AuthDesiredRequiredResourceAccess>>(
            StringComparer.OrdinalIgnoreCase);

        void Add(AuthDesiredRequiredResourceAccess entry)
        {
            if (!byResource.TryGetValue(entry.ResourceAppId, out var perms))
            {
                perms = new Dictionary<(Guid, string), AuthDesiredRequiredResourceAccess>();
                byResource[entry.ResourceAppId] = perms;
            }

            perms[(entry.PermissionId, entry.Type)] = entry;
        }

        foreach (var e in existing)
        {
            Add(e);
        }

        foreach (var d in desired)
        {
            Add(d);
        }

        return byResource.Select(kv => new RequiredResourceAccess
        {
            ResourceAppId = kv.Key,
            ResourceAccess = kv.Value.Values.Select(p => new ResourceAccess
            {
                Id = p.PermissionId,
                Type = p.Type
            }).ToList()
        }).ToList();
    }

    public static void Apply(Application application, IReadOnlyList<RequiredResourceAccess> access)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.RequiredResourceAccess = access.ToList();
    }

    private static string Key(AuthDesiredRequiredResourceAccess e) =>
        $"{e.ResourceAppId}|{e.PermissionId:D}|{e.Type}";
}
