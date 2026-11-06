using System.Security.Claims;
using Autoprint.Server.Data;
using Autoprint.Shared;
using Microsoft.EntityFrameworkCore;

namespace Autoprint.Server.Services
{
    /// <summary>
    /// Empêche l'escalade de privilèges (CWE-269) selon <see cref="PermissionRules"/> : on ne peut accorder une permission,
    /// ni gérer un rôle ou un compte qui l'a, que si l'on peut soi-même l'accorder.
    /// </summary>
    public static class PermissionGuard
    {
        public const string AdminPermission = PermissionRules.Admin;

        public static HashSet<string> GetCallerPermissions(ClaimsPrincipal user)
        {
            return user.Claims
                .Where(c => c.Type == "Permission")
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsAdmin(ClaimsPrincipal user) => GetCallerPermissions(user).Contains(AdminPermission);

        public static bool CanGrant(HashSet<string> caller, string code) => PermissionRules.CanGrant(caller, code);

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
            return codes.All(c => CanGrant(callerPermissions, c));
        }

        /// <summary>Gérer un rôle (le modifier, le supprimer, le lier à un groupe AD, l'attribuer à un compte).</summary>
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
            return codes.All(c => CanGrant(callerPermissions, c));
        }

        public static async Task<bool> CanManageUserAsync(ApplicationDbContext context, ClaimsPrincipal user, int targetUserId)
        {
            var targetRoleIds = await context.UserRoles
                .Where(ur => ur.UserId == targetUserId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            return await CanManageRolesAsync(context, user, targetRoleIds);
        }

        /// <summary>
        /// Reste-t-il au moins un compte actif disposant d'Administration, en ignorant un compte et/ou un rôle ?
        /// Empêche de retirer le dernier administrateur (application devenue ingérable).
        /// </summary>
        public static Task<bool> HasOtherActiveAdminAsync(ApplicationDbContext context, int? excludeUserId = null, int? excludeRoleId = null)
        {
            return context.UserRoles.AnyAsync(ur =>
                ur.User.IsActive
                && (excludeUserId == null || ur.UserId != excludeUserId)
                && (excludeRoleId == null || ur.RoleId != excludeRoleId)
                && ur.Role.RolePermissions.Any(rp => rp.Permission.Code == AdminPermission));
        }

        public static Task<bool> RoleHasAdminAsync(ApplicationDbContext context, int roleId)
            => context.RolePermissions.AnyAsync(rp => rp.RoleId == roleId && rp.Permission.Code == AdminPermission);

        public static Task<bool> UserIsAdminAsync(ApplicationDbContext context, int userId)
            => context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.RolePermissions.Any(rp => rp.Permission.Code == AdminPermission));
    }
}
