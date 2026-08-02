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
        EntraAuthAppRegistrationResource app,
        Func<EntraAuthAppRegistrationResource, string?> resolveExposerClientId)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(resolveExposerClientId);

        var result = new List<AuthDesiredRequiredResourceAccess>();
        foreach (var annotation in app.Annotations.OfType<ApiPermissionAnnotation>())
        {
            if (TryResolveDesired(annotation.PermissionResource, resolveExposerClientId, out var entry))
            {
                result.Add(entry);
            }
        }

        foreach (var annotation in app.Annotations.OfType<WellKnownApiPermissionAnnotation>())
        {
            if (TryResolveDesired(annotation.PermissionResource, resolveExposerClientId, out var entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    public static bool HasDeclaredPermissions(EntraAuthAppRegistrationResource app) =>
        app.Annotations.OfType<ApiPermissionAnnotation>().Any() ||
        app.Annotations.OfType<WellKnownApiPermissionAnnotation>().Any();

    /// <summary>
    /// Resolves the desired Graph entry for one permission child (with exposer-plan remap when available).
    /// </summary>
    public static bool TryResolveDesired(
        ApiPermissionResource permission,
        Func<EntraAuthAppRegistrationResource, string?> resolveExposerClientId,
        out AuthDesiredRequiredResourceAccess desired)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(resolveExposerClientId);

        if (permission.WellKnownPermission is { } wellKnown)
        {
            desired = new AuthDesiredRequiredResourceAccess
            {
                ResourceAppId = wellKnown.ResourceAppId,
                PermissionId = wellKnown.PermissionId,
                Type = wellKnown.Type
            };
            return true;
        }

        if (permission.Exposition is not { } exposition)
        {
            desired = null!;
            return false;
        }

        var resourceAppId = resolveExposerClientId(exposition.Owner);
        if (string.IsNullOrWhiteSpace(resourceAppId))
        {
            desired = null!;
            return false;
        }

        desired = new AuthDesiredRequiredResourceAccess
        {
            ResourceAppId = resourceAppId,
            PermissionId = ResolvePermissionId(exposition),
            Type = exposition is AppRoleApiExposition ? "Role" : "Scope"
        };
        return true;
    }

    /// <summary>Stable compare key: <c>resourceAppId|permissionId|type</c>.</summary>
    public static string FormatKey(AuthDesiredRequiredResourceAccess entry) => Key(entry);

    /// <summary>
    /// Permission id from exposer plan remap by value when available; otherwise model Guid.
    /// </summary>
    public static Guid ResolvePermissionId(ApiExposition exposition)
    {
        ArgumentNullException.ThrowIfNull(exposition);

        var exposerPlan = exposition.Owner.Annotations
            .OfType<AuthAppRegistrationPlanAnnotation>()
            .LastOrDefault()
            ?.Plan;

        switch (exposition)
        {
            case ScopeApiExposition scope:
                return exposerPlan?.DesiredScopes
                    .FirstOrDefault(s => string.Equals(s.Value, scope.ScopeValue, StringComparison.Ordinal))
                    ?.Id
                    ?? scope.PermissionId;
            case AppRoleApiExposition role:
                return exposerPlan?.DesiredAppRoles
                    .FirstOrDefault(r => string.Equals(r.Value, role.Value, StringComparison.Ordinal))
                    ?.Id
                    ?? role.RoleId;
            default:
                return Guid.Empty;
        }
    }

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
