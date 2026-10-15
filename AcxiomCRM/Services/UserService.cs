using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ICrmAccessService _crmAccess;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService,
            ICrmAccessService crmAccess)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
            _crmAccess = crmAccess;
        }

        public async Task<List<UserDto>> GetUsersAsync(string? search = null, string? role = null, bool? isActive = null)
        {
            var query = _userManager.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(s) ||
                    (u.Email != null && u.Email.ToLower().Contains(s)) ||
                    (u.Department != null && u.Department.ToLower().Contains(s)));
            }

            if (isActive.HasValue)
            {
                query = query.Where(u => u.IsActive == isActive.Value);
            }

            var users = await query.OrderByDescending(u => u.CreatedDate).ToListAsync();
            var dtos = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "None";

                if (!string.IsNullOrWhiteSpace(role) && !string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isLockedOut = await _userManager.IsLockedOutAsync(user);

                dtos.Add(new UserDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    Department = user.Department,
                    Role = userRole,
                    IsActive = user.IsActive,
                    IsLockedOut = isLockedOut,
                    CreatedDate = user.CreatedDate
                });
            }

            return dtos;
        }

        public async Task<UserDto?> GetUserByIdAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var isLockedOut = await _userManager.IsLockedOutAsync(user);

            return new UserDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Department = user.Department,
                Role = roles.FirstOrDefault() ?? "None",
                IsActive = user.IsActive,
                IsLockedOut = isLockedOut,
                CreatedDate = user.CreatedDate
            };
        }

        public async Task<(bool Success, string? Error)> CreateUserAsync(
            CreateUserModel model,
            ClaimsPrincipal adminUser,
            string? ipAddress = null)
        {
            if (!_crmAccess.IsAdmin(adminUser))
            {
                return (false, "Only administrators can create users.");
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                return (false, "Email is required.");
            }

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                return (false, "Full Name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Role) || !AppRoles.AllRoles.Contains(model.Role))
            {
                return (false, $"Invalid role. Allowed roles are: {string.Join(", ", AppRoles.AllRoles)}");
            }

            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
            {
                return (false, "Password must be at least 8 characters long.");
            }

            var existing = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (existing != null)
            {
                return (false, "A user with this email address already exists.");
            }

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                Department = model.Department?.Trim(),
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, model.Role);

            var adminId = _crmAccess.GetCurrentUserId(adminUser);
            await _auditService.LogAsync(
                adminId,
                "CreateUser",
                "User",
                user.Id,
                null,
                $"Email={user.Email}, Role={model.Role}, Name={user.FullName}",
                $"Created user {user.Email} with role {model.Role}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UpdateUserAsync(
            EditUserModel model,
            ClaimsPrincipal adminUser,
            string? ipAddress = null)
        {
            if (!_crmAccess.IsAdmin(adminUser))
            {
                return (false, "Only administrators can edit users.");
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return (false, "User not found.");
            }

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                return (false, "Full Name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.Role) || !AppRoles.AllRoles.Contains(model.Role))
            {
                return (false, $"Invalid role. Allowed roles are: {string.Join(", ", AppRoles.AllRoles)}");
            }

            // Protect last active administrator from losing Admin role
            var currentRoles = await _userManager.GetRolesAsync(user);
            var isCurrentlyAdmin = currentRoles.Contains(AppRoles.Admin);
            if (isCurrentlyAdmin && model.Role != AppRoles.Admin)
            {
                var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
                var activeAdmins = admins.Count(a => a.IsActive && a.Id != user.Id);
                if (activeAdmins == 0)
                {
                    return (false, "Cannot remove the Admin role from the last active administrator.");
                }
            }

            var oldSummary = $"Name={user.FullName}, Dept={user.Department}, Role={currentRoles.FirstOrDefault()}, Active={user.IsActive}";

            user.FullName = model.FullName.Trim();
            user.Department = model.Department?.Trim();
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            }

            // Update role if changed
            if (!currentRoles.Contains(model.Role))
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            var newSummary = $"Name={user.FullName}, Dept={user.Department}, Role={model.Role}, Active={user.IsActive}";
            var adminId = _crmAccess.GetCurrentUserId(adminUser);

            await _auditService.LogAsync(
                adminId,
                "UpdateUser",
                "User",
                user.Id,
                oldSummary,
                newSummary,
                $"Updated user {user.Email}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ToggleUserStatusAsync(
            string id,
            ClaimsPrincipal adminUser,
            string? ipAddress = null)
        {
            if (!_crmAccess.IsAdmin(adminUser))
            {
                return (false, "Only administrators can toggle user status.");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return (false, "User not found.");
            }

            // Protect last active admin from being deactivated
            if (user.IsActive)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(AppRoles.Admin))
                {
                    var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
                    var activeAdmins = admins.Count(a => a.IsActive && a.Id != user.Id);
                    if (activeAdmins == 0)
                    {
                        return (false, "Cannot deactivate the last active administrator.");
                    }
                }
            }

            var oldStatus = user.IsActive;
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var adminId = _crmAccess.GetCurrentUserId(adminUser);
            await _auditService.LogAsync(
                adminId,
                user.IsActive ? "ActivateUser" : "DeactivateUser",
                "User",
                user.Id,
                $"IsActive={oldStatus}",
                $"IsActive={user.IsActive}",
                $"Toggled account status for {user.Email}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> UnlockUserAsync(
            string id,
            ClaimsPrincipal adminUser,
            string? ipAddress = null)
        {
            if (!_crmAccess.IsAdmin(adminUser))
            {
                return (false, "Only administrators can unlock accounts.");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return (false, "User not found.");
            }

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            var adminId = _crmAccess.GetCurrentUserId(adminUser);
            await _auditService.LogAsync(
                adminId,
                "UnlockUser",
                "User",
                user.Id,
                "LockedOut=True",
                "LockedOut=False",
                $"Admin unlocked user {user.Email}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ResetPasswordAsync(
            string id,
            string newPassword,
            ClaimsPrincipal adminUser,
            string? ipAddress = null)
        {
            if (!_crmAccess.IsAdmin(adminUser))
            {
                return (false, "Only administrators can reset passwords.");
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                return (false, "Password must be at least 8 characters long.");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return (false, "User not found.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (!result.Succeeded)
            {
                return (false, string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            var adminId = _crmAccess.GetCurrentUserId(adminUser);
            await _auditService.LogAsync(
                adminId,
                "ResetPassword",
                "User",
                user.Id,
                null,
                "PasswordResetByAdmin",
                $"Admin reset password for user {user.Email}",
                ipAddress);

            return (true, null);
        }
    }
}
