using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
    public class AuditLogsController : Controller
    {
        private readonly IAuditService _auditService;
        private readonly UserManager<Models.ApplicationUser> _userManager;

        public AuditLogsController(
            IAuditService auditService,
            UserManager<Models.ApplicationUser> userManager)
        {
            _auditService = auditService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(
            string? userId = null,
            string? module = null,
            string? action = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var logs = await _auditService.GetLogsAsync(userId, module, action, fromDate, toDate, 150);

            var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
            ViewBag.Users = new SelectList(users, "Id", "FullName", userId);

            ViewBag.UserId = userId;
            ViewBag.Module = module;
            ViewBag.Action = action;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;

            return View(logs);
        }
    }
}
