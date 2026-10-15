using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        public async Task<IActionResult> Index(string? search = null, string? role = null, bool? isActive = null)
        {
            var users = await _userService.GetUsersAsync(search, role, isActive);
            ViewBag.Search = search;
            ViewBag.Role = role;
            ViewBag.IsActive = isActive;
            ViewBag.Roles = new SelectList(AppRoles.AllRoles, role);

            return View(users);
        }

        public IActionResult Create()
        {
            ViewBag.Roles = new SelectList(AppRoles.AllRoles);
            return View(new CreateUserModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserModel model)
        {
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "Password and confirmation do not match.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(AppRoles.AllRoles, model.Role);
                return View(model);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _userService.CreateUserAsync(model, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to create user.");
                ViewBag.Roles = new SelectList(AppRoles.AllRoles, model.Role);
                return View(model);
            }

            TempData["SuccessMessage"] = $"User '{model.Email}' created successfully with role {model.Role}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = new SelectList(AppRoles.AllRoles, user.Role);
            var model = new EditUserModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Department = user.Department,
                Role = user.Role,
                IsActive = user.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = new SelectList(AppRoles.AllRoles, model.Role);
                return View(model);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _userService.UpdateUserAsync(model, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to update user.");
                ViewBag.Roles = new SelectList(AppRoles.AllRoles, model.Role);
                return View(model);
            }

            TempData["SuccessMessage"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _userService.ToggleUserStatusAsync(id, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to update user status.";
            }
            else
            {
                TempData["SuccessMessage"] = "User account status updated.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _userService.UnlockUserAsync(id, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to unlock user.";
            }
            else
            {
                TempData["SuccessMessage"] = "User account unlocked successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword != confirmPassword)
            {
                TempData["ErrorMessage"] = "Passwords do not match or are invalid.";
                return RedirectToAction(nameof(Index));
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _userService.ResetPasswordAsync(id, newPassword, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to reset password.";
            }
            else
            {
                TempData["SuccessMessage"] = "User password has been reset successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
