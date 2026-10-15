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
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICrmAccessService _crmAccess;

        public CustomersController(
            ICustomerService customerService,
            UserManager<ApplicationUser> userManager,
            ICrmAccessService crmAccess)
        {
            _customerService = customerService;
            _userManager = userManager;
            _crmAccess = crmAccess;
        }

        public async Task<IActionResult> Index(
            string? search = null,
            CustomerStatus? status = null,
            string? ownerId = null,
            string? sortBy = "createdDate",
            bool descending = true,
            int page = 1,
            int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _customerService.GetCustomersAsync(
                User, search, status, ownerId, sortBy, descending, page, pageSize);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.OwnerId = ownerId;
            ViewBag.SortBy = sortBy;
            ViewBag.Descending = descending;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
                ViewBag.Owners = new SelectList(users, "Id", "FullName", ownerId);
            }

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id, User);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer not found or you do not have permission to view it.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        public IActionResult Create()
        {
            PopulateOwnersDropdown();
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            // Remove navigation and non-essential model errors
            ModelState.Remove(nameof(customer.CustomerCode));
            ModelState.Remove(nameof(customer.CreatedBy));
            ModelState.Remove(nameof(customer.Owner));
            ModelState.Remove(nameof(customer.OwnerId));

            if (!ModelState.IsValid)
            {
                PopulateOwnersDropdown(customer.OwnerId);
                return View(customer);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, created) = await _customerService.CreateCustomerAsync(customer, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to create customer.");
                PopulateOwnersDropdown(customer.OwnerId);
                return View(customer);
            }

            TempData["SuccessMessage"] = $"Customer '{created!.CustomerName}' ({created.CustomerCode}) was created successfully.";
            return RedirectToAction(nameof(Details), new { id = created.CustomerId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(id, User);
            if (customer == null)
            {
                TempData["ErrorMessage"] = "Customer not found or you do not have permission to edit it.";
                return RedirectToAction(nameof(Index));
            }

            PopulateOwnersDropdown(customer.OwnerId);
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.CustomerId)
            {
                return BadRequest();
            }

            ModelState.Remove(nameof(customer.CustomerCode));
            ModelState.Remove(nameof(customer.CreatedBy));
            ModelState.Remove(nameof(customer.Owner));
            ModelState.Remove(nameof(customer.OwnerId));

            if (!ModelState.IsValid)
            {
                PopulateOwnersDropdown(customer.OwnerId);
                return View(customer);
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error, updated) = await _customerService.UpdateCustomerAsync(customer, User, ip);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to update customer.");
                PopulateOwnersDropdown(customer.OwnerId);
                return View(customer);
            }

            TempData["SuccessMessage"] = $"Customer '{updated!.CustomerName}' was updated successfully.";
            return RedirectToAction(nameof(Details), new { id = updated.CustomerId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _customerService.DeleteCustomerAsync(id, User, ip);

            if (!success)
            {
                TempData["ErrorMessage"] = error ?? "Failed to delete customer.";
            }
            else
            {
                TempData["SuccessMessage"] = "Customer was deleted or deactivated successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private void PopulateOwnersDropdown(string? selectedId = null)
        {
            if (_crmAccess.IsAdmin(User) || _crmAccess.IsManager(User))
            {
                var users = _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToList();
                ViewBag.Owners = new SelectList(users, "Id", "FullName", selectedId);
            }
        }
    }
}
