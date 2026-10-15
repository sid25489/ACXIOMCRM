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
    public class FollowUpsController : Controller
    {
        private readonly IFollowUpService _followUpService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAccessService _crmAccess;

        public FollowUpsController(
            IFollowUpService followUpService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAccessService crmAccess)
        {
            _followUpService = followUpService;
            _context = context;
            _userManager = userManager;
            _crmAccess = crmAccess;
        }

        public async Task<IActionResult> Index(
            FollowUpStatus? status = null,
            FollowUpType? type = null,
            string? assignedTo = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _followUpService.GetFollowUpsAsync(
                User, status, type, assignedTo, fromDate, toDate, page, pageSize);

            var overdue = await _followUpService.GetOverdueFollowUpsAsync(User);
            var upcoming = await _followUpService.GetUpcomingFollowUpsAsync(User, 7);

            ViewBag.Status = status;
            ViewBag.Type = type;
            ViewBag.AssignedTo = assignedTo;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.OverdueCount = overdue.Count;
            ViewBag.UpcomingCount = upcoming.Count;

            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
                ViewBag.Users = new SelectList(users, "Id", "FullName", assignedTo);
            }

            return View(items);
        }

        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
        {
            await PopulateDropdownsAsync(customerId, leadId, opportunityId);

            var model = new FollowUp
            {
                FollowUpDate = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
                FollowUpType = FollowUpType.Call,
                Status = FollowUpStatus.Planned
            };

            if (customerId.HasValue) model.CustomerId = customerId.Value;
            if (leadId.HasValue) model.LeadId = leadId.Value;
            if (opportunityId.HasValue) model.OpportunityId = opportunityId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUp followUp)
        {
            ModelState.Remove(nameof(followUp.CreatedBy));
            ModelState.Remove(nameof(followUp.AssignedUser));
            ModelState.Remove(nameof(followUp.AssignedTo));
            ModelState.Remove(nameof(followUp.Customer));
            ModelState.Remove(nameof(followUp.Lead));
            ModelState.Remove(nameof(followUp.Opportunity));

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
                return View(followUp);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, created) = await _followUpService.CreateFollowUpAsync(followUp, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to schedule follow-up.");
                await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
                return View(followUp);
            }

            TempData["SuccessMessage"] = $"Follow-up '{created!.Subject}' scheduled for {created.FollowUpDate:dd-MMM-yyyy}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var followUp = await _followUpService.GetFollowUpByIdAsync(id, User);
            if (followUp == null)
            {
                TempData["ErrorMessage"] = "Follow-up not found or you do not have permission to edit it.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
            return View(followUp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FollowUp followUp)
        {
            if (id != followUp.FollowUpId)
            {
                return BadRequest();
            }

            ModelState.Remove(nameof(followUp.CreatedBy));
            ModelState.Remove(nameof(followUp.AssignedUser));
            ModelState.Remove(nameof(followUp.AssignedTo));
            ModelState.Remove(nameof(followUp.Customer));
            ModelState.Remove(nameof(followUp.Lead));
            ModelState.Remove(nameof(followUp.Opportunity));

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
                return View(followUp);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, updated) = await _followUpService.UpdateFollowUpAsync(followUp, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to update follow-up.");
                await PopulateDropdownsAsync(followUp.CustomerId, followUp.LeadId, followUp.OpportunityId, followUp.AssignedTo);
                return View(followUp);
            }

            TempData["SuccessMessage"] = $"Follow-up '{updated!.Subject}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string? remarks)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _followUpService.CompleteFollowUpAsync(id, remarks, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to complete follow-up.";
            }
            else
            {
                TempData["SuccessMessage"] = "Follow-up marked as Completed.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, DateTime newDate, string? remarks)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _followUpService.RescheduleFollowUpAsync(id, newDate, remarks, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to reschedule follow-up.";
            }
            else
            {
                TempData["SuccessMessage"] = $"Follow-up rescheduled to {newDate:dd-MMM-yyyy}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _followUpService.CancelFollowUpAsync(id, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to cancel follow-up.";
            }
            else
            {
                TempData["SuccessMessage"] = "Follow-up has been Cancelled.";
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
