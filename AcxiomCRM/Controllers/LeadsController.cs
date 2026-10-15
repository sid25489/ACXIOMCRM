using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class LeadsController : Controller
    {
        private readonly ILeadService _leadService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAccessService _crmAccess;

        public LeadsController(
            ILeadService leadService,
            UserManager<ApplicationUser> userManager,
            ICrmAccessService crmAccess)
        {
            _leadService = leadService;
            _userManager = userManager;
            _crmAccess = crmAccess;
        }

        public async Task<IActionResult> Index(
            string? search = null,
            LeadStatus? status = null,
            LeadSource? source = null,
            string? assignedTo = null,
            string? sortBy = "createdDate",
            bool descending = true,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _leadService.GetLeadsAsync(
                User, search, status, source, assignedTo, sortBy, descending, page, pageSize);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Source = source;
            ViewBag.AssignedTo = assignedTo;
            ViewBag.SortBy = sortBy;
            ViewBag.Descending = descending;
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
            var lead = await _leadService.GetLeadByIdAsync(id, User);
            if (lead == null)
            {
                TempData["ErrorMessage"] = "Lead not found or you do not have permission to view it.";
                return RedirectToAction(nameof(Index));
            }

            return View(lead);
        }

        public IActionResult Create()
        {
            PopulateUsersDropdown();
            return View(new Lead());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            ModelState.Remove(nameof(lead.LeadCode));
            ModelState.Remove(nameof(lead.CreatedBy));
            ModelState.Remove(nameof(lead.AssignedUser));
            ModelState.Remove(nameof(lead.AssignedTo));
            ModelState.Remove(nameof(lead.Customer));

            if (!ModelState.IsValid)
            {
                PopulateUsersDropdown(lead.AssignedTo);
                return View(lead);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, created) = await _leadService.CreateLeadAsync(lead, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to create lead.");
                PopulateUsersDropdown(lead.AssignedTo);
                return View(lead);
            }

            TempData["SuccessMessage"] = $"Lead '{created!.LeadName}' ({created.LeadCode}) created successfully.";
            return RedirectToAction(nameof(Details), new { id = created.LeadId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var lead = await _leadService.GetLeadByIdAsync(id, User);
            if (lead == null)
            {
                TempData["ErrorMessage"] = "Lead not found or you do not have permission to edit it.";
                return RedirectToAction(nameof(Index));
            }

            if (lead.Status == LeadStatus.Converted)
            {
                TempData["ErrorMessage"] = "A converted lead cannot be edited.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }

            PopulateUsersDropdown(lead.AssignedTo);
            return View(lead);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.LeadId)
            {
                return BadRequest();
            }

            ModelState.Remove(nameof(lead.LeadCode));
            ModelState.Remove(nameof(lead.CreatedBy));
            ModelState.Remove(nameof(lead.AssignedUser));
            ModelState.Remove(nameof(lead.AssignedTo));
            ModelState.Remove(nameof(lead.Customer));

            if (!ModelState.IsValid)
            {
                PopulateUsersDropdown(lead.AssignedTo);
                return View(lead);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, updated) = await _leadService.UpdateLeadAsync(lead, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to update lead.");
                PopulateUsersDropdown(lead.AssignedTo);
                return View(lead);
            }

            TempData["SuccessMessage"] = $"Lead '{updated!.LeadName}' updated successfully.";
            return RedirectToAction(nameof(Details), new { id = updated.LeadId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(int leadId, LeadStatus newStatus)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _leadService.ChangeStatusAsync(leadId, newStatus, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = $"Lead status updated to {newStatus}.";
            }

            return RedirectToAction(nameof(Details), new { id = leadId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(ConvertLeadModel model)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, customer, opp) = await _leadService.ConvertLeadAsync(model, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to convert lead.";
                return RedirectToAction(nameof(Details), new { id = model.LeadId });
            }

            TempData["SuccessMessage"] = $"Lead successfully converted! Customer '{customer!.CustomerName}' was created/linked." +
                (opp != null ? $" Opportunity '{opp.OpportunityName}' with amount ₹{opp.Amount:N2} was generated." : "");

            return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _leadService.DeleteLeadAsync(id, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to delete lead.";
            }
            else
            {
                TempData["SuccessMessage"] = "Lead was deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private void PopulateUsersDropdown(string? selectedId = null)
        {
            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
                ViewBag.Users = new SelectList(users, "Id", "FullName", selectedId);
            }
        }
    }
}
