using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] ApiLoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                await _auditService.LogAsync(null, "ApiFailedLogin", "AuthApi", null, null, null, $"API login attempt with unknown email: {request.Email}", ip, "Failed");
                return Unauthorized(new { message = "Invalid email or password." });
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync(user.Id, "ApiDisabledLoginAttempt", "AuthApi", user.Id, null, null, $"Deactivated account attempted API login: {user.Email}", ip, "Failed");
                return StatusCode(403, new { message = "Account is deactivated. Contact system administrator." });
            }

            var result = await _signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync(user.Id, "ApiLogin", "AuthApi", user.Id, null, null, $"API login success for {user.Email}", ip, "Success");

                return Ok(new ApiLoginResponse
                {
                    Success = true,
                    Message = "Authentication successful.",
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = roles.FirstOrDefault()
                });
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(user.Id, "ApiAccountLockout", "AuthApi", user.Id, null, null, $"Account locked out via API: {user.Email}", ip, "Lockout");
                return StatusCode(429, new { message = "Account is locked out due to multiple failed login attempts. Try again later." });
            }

            await _auditService.LogAsync(user.Id, "ApiFailedLogin", "AuthApi", user.Id, null, null, $"API incorrect password attempt for {user.Email}", ip, "Failed");
            return Unauthorized(new { message = "Invalid email or password." });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = _userManager.GetUserId(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _signInManager.SignOutAsync();
            await _auditService.LogAsync(userId, "ApiLogout", "AuthApi", userId, null, null, "User logged out via API", ip, "Success");

            return Ok(new { success = true, message = "Logged out successfully." });
        }
    }
}
