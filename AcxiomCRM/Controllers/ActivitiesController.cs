using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ActivitiesController : Controller
    {
        private readonly IActivityService _activityService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAccessService _crmAccess;

        public ActivitiesController(
            IActivityService activityService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAccessService crmAccess)
        {
            _activityService = activityService;
            _context = context;
            _userManager = userManager;
            _crmAccess = crmAccess;
        }

        public async Task<IActionResult> Index(
            ActivityType? type = null,
            ActivityStatus? status = null,
            string? assignedTo = null,
            int? customerId = null,
            int? leadId = null,
            int? opportunityId = null,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _activityService.GetActivitiesAsync(
                User, type, status, assignedTo, customerId, leadId, opportunityId, page, pageSize);

            ViewBag.Type = type;
            ViewBag.Status = status;
            ViewBag.AssignedTo = assignedTo;
            ViewBag.CustomerId = customerId;
            ViewBag.LeadId = leadId;
            ViewBag.OpportunityId = opportunityId;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
                ViewBag.Users = new SelectList(users, "Id", "FullName", assignedTo);
            }

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var activity = await _activityService.GetActivityByIdAsync(id, User);
            if (activity == null)
            {
                TempData["ErrorMessage"] = "Activity not found or you do not have permission to view it.";
                return RedirectToAction(nameof(Index));
            }

            return View(activity);
        }

        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
        {
            await PopulateDropdownsAsync(customerId, leadId, opportunityId);

            var model = new Activity
            {
                ActivityDate = DateTime.UtcNow,
                ActivityType = ActivityType.Task,
                Status = ActivityStatus.Completed
            };

            if (customerId.HasValue) model.CustomerId = customerId.Value;
            if (leadId.HasValue) model.LeadId = leadId.Value;
            if (opportunityId.HasValue) model.OpportunityId = opportunityId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Activity activity)
        {
            ModelState.Remove(nameof(activity.CreatedBy));
            ModelState.Remove(nameof(activity.AssignedUser));
            ModelState.Remove(nameof(activity.AssignedTo));
            ModelState.Remove(nameof(activity.Customer));
            ModelState.Remove(nameof(activity.Lead));
            ModelState.Remove(nameof(activity.Opportunity));

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.OpportunityId, activity.AssignedTo);
                return View(activity);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, created) = await _activityService.CreateActivityAsync(activity, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to log activity.");
                await PopulateDropdownsAsync(activity.CustomerId, activity.LeadId, activity.OpportunityId, activity.AssignedTo);
                return View(activity);
            }

            TempData["SuccessMessage"] = $"Activity '{created!.Subject}' recorded successfully.";

            if (created.CustomerId.HasValue)
            {
                return RedirectToAction("Details", "Customers", new { id = created.CustomerId.Value });
            }
            if (created.LeadId.HasValue)
            {
                return RedirectToAction("Details", "Leads", new { id = created.LeadId.Value });
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync(int? customerId = null, int? leadId = null, int? opportunityId = null, string? assignedTo = null)
        {
            var custQuery = _crmAccess.FilterCustomers(_context.Customers.AsNoTracking(), User);
            var customers = await custQuery.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName", customerId);

            var leadQuery = _crmAccess.FilterLeads(_context.Leads.AsNoTracking(), User);
            var leads = await leadQuery.OrderBy(l => l.LeadName).ToListAsync();
            ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName", leadId);

            var oppQuery = _crmAccess.FilterOpportunities(_context.Opportunities.AsNoTracking(), User);
            var opps = await oppQuery.OrderBy(o => o.OpportunityName).ToListAsync();
            ViewBag.Opportunities = new SelectList(opps, "OpportunityId", "OpportunityName", opportunityId);

            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
                ViewBag.Users = new SelectList(users, "Id", "FullName", assignedTo);
            }
        }
    }
}
