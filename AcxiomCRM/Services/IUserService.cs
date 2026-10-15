using AcxiomCRM.Models;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsLockedOut { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateUserModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class EditUserModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public interface IUserService
    {
        Task<List<UserDto>> GetUsersAsync(string? search = null, string? role = null, bool? isActive = null);
        Task<UserDto?> GetUserByIdAsync(string id);
        Task<(bool Success, string? Error)> CreateUserAsync(CreateUserModel model, ClaimsPrincipal adminUser, string? ipAddress = null);
        Task<(bool Success, string? Error)> UpdateUserAsync(EditUserModel model, ClaimsPrincipal adminUser, string? ipAddress = null);
        Task<(bool Success, string? Error)> ToggleUserStatusAsync(string id, ClaimsPrincipal adminUser, string? ipAddress = null);
        Task<(bool Success, string? Error)> UnlockUserAsync(string id, ClaimsPrincipal adminUser, string? ipAddress = null);
        Task<(bool Success, string? Error)> ResetPasswordAsync(string id, string newPassword, ClaimsPrincipal adminUser, string? ipAddress = null);
    }
}
