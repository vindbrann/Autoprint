using System.Security.Claims;
using Autoprint.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Empêche l'escalade de privilèges (CWE-269) : un appelant ne peut attribuer, créer ou modifier
    /// que des rôles / comptes dont les permissions sont incluses dans les siennes.
    /// </summary>
    public static class PermissionGuard
    {
        public static HashSet<string> GetCallerPermissions(ClaimsPrincipal user)
        {
            return user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static async Task<bool> CanGrantPermissionIdsAsync(ApplicationDbContext context, ClaimsPrincipal user, IEnumerable<int> permissionIds)
        {
            var ids = permissionIds.Distinct().ToList();
            if (ids.Count == 0) return true;

            var codes = await context.Permissions
                .Where(p => ids.Contains(p.Id))
                .Select(p => p.Code)
                .ToListAsync();

            // Un identifiant inconnu est refusé plutôt qu'ignoré
            if (codes.Count != ids.Count) return false;

            var callerPermissions = GetCallerPermissions(user);
            return codes.All(callerPermissions.Contains);
        }

        public static async Task<bool> CanManageRolesAsync(ApplicationDbContext context, ClaimsPrincipal user, IEnumerable<int> roleIds)
        {
            var ids = roleIds.Distinct().ToList();
            if (ids.Count == 0) return true;

            var codes = await context.RolePermissions
                .Where(rp => ids.Contains(rp.RoleId))
                .Select(rp => rp.Permission.Code)
                .Distinct()
                .ToListAsync();

            var callerPermissions = GetCallerPermissions(user);
            return codes.All(callerPermissions.Contains);
        }

        public static async Task<bool> CanManageUserAsync(ApplicationDbContext context, ClaimsPrincipal user, int targetUserId)
        {
            var targetRoleIds = await context.UserRoles
                .Where(ur => ur.UserId == targetUserId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            return await CanManageRolesAsync(context, user, targetRoleIds);
        }
    }
}
