using Microsoft.EntityFrameworkCore;
using Optical.Application.Abstractions.Persistence;
using Optical.Application.Common;
using Optical.Domain.Entities;
using Optical.Domain.Exceptions;

namespace Optical.Application.Features.Permissions;

/// <summary>
/// Reads and writes which role holds which module permission.
///
/// Registered scoped and cached for the lifetime of one request: a page checks several
/// permissions while rendering, and they must all see the same answer without costing a
/// query each. Nothing is cached beyond the request, so a save takes effect on the very
/// next one and there is no invalidation to get wrong.
/// </summary>
public sealed class RolePermissionStore(IApplicationDbContext db)
{
    private Dictionary<string, HashSet<string>>? cached;

    /// <summary>True when any of the user's roles grants the permission.</summary>
    public async Task<bool> IsGrantedAsync(
        IEnumerable<string> roles,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var matrix = await LoadAsync(cancellationToken);

        return roles.Any(role => matrix.TryGetValue(role, out var granted) && granted.Contains(permission));
    }

    /// <summary>Every role's grants, for the editing screen.</summary>
    public async Task<IReadOnlyDictionary<string, HashSet<string>>> GetMatrixAsync(
        CancellationToken cancellationToken = default) =>
        await LoadAsync(cancellationToken);

    /// <summary>
    /// Replaces one role's grants wholesale. Unknown keys are refused, and Admin keeps the
    /// staff-management permission whatever the form says.
    /// </summary>
    public async Task SaveAsync(
        string role,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new DomainException("That role does not exist.");
        }

        var wanted = permissions.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        foreach (var permission in wanted)
        {
            if (!AppPermissions.Exists(permission))
            {
                throw new DomainException($"\"{permission}\" is not a known module.");
            }
        }

        // The last line of defence against an Admin locking every human out.
        foreach (var definition in AppPermissions.All)
        {
            if (AppPermissions.IsLocked(role, definition.Key))
            {
                wanted.Add(definition.Key);
            }
        }

        var existing = await db.RolePermissions
            .Where(p => p.Role == role)
            .ToListAsync(cancellationToken);

        foreach (var row in existing.Where(row => !wanted.Contains(row.Permission)))
        {
            db.RolePermissions.Remove(row);
        }

        var already = existing.Select(row => row.Permission).ToHashSet(StringComparer.Ordinal);

        foreach (var permission in wanted.Where(permission => !already.Contains(permission)))
        {
            db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
        }

        await db.SaveChangesAsync(cancellationToken);

        cached = null;
    }

    private async Task<Dictionary<string, HashSet<string>>> LoadAsync(CancellationToken cancellationToken)
    {
        if (cached is not null)
        {
            return cached;
        }

        var rows = await db.RolePermissions
            .AsNoTracking()
            .Select(p => new { p.Role, p.Permission })
            .ToListAsync(cancellationToken);

        cached = rows
            .GroupBy(r => r.Role, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Permission).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

        return cached;
    }
}
