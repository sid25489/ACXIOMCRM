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
    public class OpportunitiesController : Controller
    {
        private readonly IOpportunityService _opportunityService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAccessService _crmAccess;

        public OpportunitiesController(
            IOpportunityService opportunityService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ICrmAccessService crmAccess)
        {
            _opportunityService = opportunityService;
            _context = context;
            _userManager = userManager;
            _crmAccess = crmAccess;
        }

        public async Task<IActionResult> Index(
            string? search = null,
            OpportunityStage? stage = null,
            OpportunityStatus? status = null,
            string? assignedTo = null,
            int? customerId = null,
            string? sortBy = "createdDate",
            bool descending = true,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _opportunityService.GetOpportunitiesAsync(
                User, search, stage, status, assignedTo, customerId, sortBy, descending, page, pageSize);

            var (totalPipe, weightedPipe, openDeals, wonDeals, lostDeals) = await _opportunityService.GetPipelineSummaryAsync(User);

            ViewBag.Search = search;
            ViewBag.Stage = stage;
            ViewBag.Status = status;
            ViewBag.AssignedTo = assignedTo;
            ViewBag.CustomerId = customerId;
            ViewBag.SortBy = sortBy;
            ViewBag.Descending = descending;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.TotalPipeline = totalPipe;
            ViewBag.WeightedPipeline = weightedPipe;
            ViewBag.OpenDeals = openDeals;
            ViewBag.WonDeals = wonDeals;
            ViewBag.LostDeals = lostDeals;

            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
                ViewBag.Users = new SelectList(users, "Id", "FullName", assignedTo);
            }

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var opp = await _opportunityService.GetOpportunityByIdAsync(id, User);
            if (opp == null)
            {
                TempData["ErrorMessage"] = "Opportunity not found or you do not have permission to view it.";
                return RedirectToAction(nameof(Index));
            }

            return View(opp);
        }

        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            await PopulateDropdownsAsync(customerId, leadId);

            var model = new Opportunity
            {
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30),
                Probability = 20,
                Stage = OpportunityStage.Qualification
            };

            if (customerId.HasValue) model.CustomerId = customerId.Value;
            if (leadId.HasValue) model.LeadId = leadId.Value;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Opportunity opportunity)
        {
            ModelState.Remove(nameof(opportunity.CreatedBy));
            ModelState.Remove(nameof(opportunity.AssignedUser));
            ModelState.Remove(nameof(opportunity.AssignedTo));
            ModelState.Remove(nameof(opportunity.Customer));
            ModelState.Remove(nameof(opportunity.Lead));

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
                return View(opportunity);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, created) = await _opportunityService.CreateOpportunityAsync(opportunity, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to create opportunity.");
                await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
                return View(opportunity);
            }

            TempData["SuccessMessage"] = $"Opportunity '{created!.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Details), new { id = created.OpportunityId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var opp = await _opportunityService.GetOpportunityByIdAsync(id, User);
            if (opp == null)
            {
                TempData["ErrorMessage"] = "Opportunity not found or you do not have permission to edit it.";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(opp.CustomerId, opp.LeadId, opp.AssignedTo);
            return View(opp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Opportunity opportunity)
        {
            if (id != opportunity.OpportunityId)
            {
                return BadRequest();
            }

            ModelState.Remove(nameof(opportunity.CreatedBy));
            ModelState.Remove(nameof(opportunity.AssignedUser));
            ModelState.Remove(nameof(opportunity.AssignedTo));
            ModelState.Remove(nameof(opportunity.Customer));
            ModelState.Remove(nameof(opportunity.Lead));

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
                return View(opportunity);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, updated) = await _opportunityService.UpdateOpportunityAsync(opportunity, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to update opportunity.");
                await PopulateDropdownsAsync(opportunity.CustomerId, opportunity.LeadId, opportunity.AssignedTo);
                return View(opportunity);
            }

            TempData["SuccessMessage"] = $"Opportunity '{updated!.OpportunityName}' updated successfully.";
            return RedirectToAction(nameof(Details), new { id = updated.OpportunityId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStage(int opportunityId, OpportunityStage newStage)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _opportunityService.ChangeStageAsync(opportunityId, newStage, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = $"Opportunity moved to {newStage} stage.";
            }

            return RedirectToAction(nameof(Details), new { id = opportunityId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _opportunityService.DeleteOpportunityAsync(id, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to delete opportunity.";
            }
            else
            {
                TempData["SuccessMessage"] = "Opportunity was deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync(int? customerId = null, int? leadId = null, string? assignedTo = null)
        {
            var custQuery = _crmAccess.FilterCustomers(_context.Customers.AsNoTracking(), User);
            var customers = await custQuery.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName", customerId);

            var leadQuery = _crmAccess.FilterLeads(_context.Leads.AsNoTracking(), User);
            var leads = await leadQuery.OrderBy(l => l.LeadName).ToListAsync();
            ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName", leadId);

            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
                ViewBag.Users = new SelectList(users, "Id", "FullName", assignedTo);
            }
        }
    }
}
