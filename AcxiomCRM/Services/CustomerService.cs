using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;
        private readonly IAuditService _auditService;
        private readonly IDuplicateDetectionService _duplicateDetection;

        public CustomerService(
            ApplicationDbContext context,
            ICrmAccessService crmAccess,
            IAuditService auditService,
            IDuplicateDetectionService duplicateDetection)
        {
            _context = context;
            _crmAccess = crmAccess;
            _auditService = auditService;
            _duplicateDetection = duplicateDetection;
        }

        public async Task<bool> EmailExistsAsync(string email, int? excludeCustomerId = null)
        {
            var normalized = _duplicateDetection.NormalizeEmail(email);
            var query = _context.Customers.AsQueryable();
            if (excludeCustomerId.HasValue)
            {
                query = query.Where(c => c.CustomerId != excludeCustomerId.Value);
            }
            return await query.AnyAsync(c => c.Email.ToLower() == normalized);
        }

        public async Task<bool> PhoneExistsAsync(string phone, int? excludeCustomerId = null)
        {
            var normalized = _duplicateDetection.NormalizePhone(phone);
            var query = _context.Customers.AsQueryable();
            if (excludeCustomerId.HasValue)
            {
                query = query.Where(c => c.CustomerId != excludeCustomerId.Value);
            }
            return await query.AnyAsync(c => c.Phone == normalized);
        }

        public async Task<(bool Success, string? Error, Customer? Customer)> CreateCustomerAsync(Customer customer, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            // Validation
            if (string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                return (false, "Customer name is required.", null);
            }

            if (string.IsNullOrWhiteSpace(customer.Email) || !_duplicateDetection.IsValidEmail(customer.Email))
            {
                return (false, "Enter a valid email address.", null);
            }

            if (string.IsNullOrWhiteSpace(customer.Phone) || !_duplicateDetection.IsValidPhone(customer.Phone))
            {
                return (false, "Enter a valid 10-digit Indian mobile number (e.g., 9876543210).", null);
            }

            customer.Email = _duplicateDetection.NormalizeEmail(customer.Email);
            customer.Phone = _duplicateDetection.NormalizePhone(customer.Phone);

            // Duplicate checks
            if (await EmailExistsAsync(customer.Email))
            {
                return (false, "This email address is already registered.", null);
            }

            if (await PhoneExistsAsync(customer.Phone))
            {
                return (false, "This phone number is already registered.", null);
            }

            // Assign ownership: SalesExecutive cannot set owner to another user
            if (_crmAccess.IsSalesExecutive(user) || string.IsNullOrEmpty(customer.OwnerId))
            {
                customer.OwnerId = userId;
            }

            // Generate Customer Code if not set
            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            {
                var maxId = await _context.Customers.Select(c => (int?)c.CustomerId).MaxAsync() ?? 0;
                customer.CustomerCode = $"CUST-{(maxId + 1):D5}";
            }

            customer.CreatedDate = DateTime.UtcNow;
            customer.CreatedBy = user.Identity?.Name ?? "User";

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Create",
                "Customer",
                customer.CustomerId.ToString(),
                null,
                $"Code={customer.CustomerCode}, Name={customer.CustomerName}, Email={customer.Email}",
                $"Created customer {customer.CustomerName}",
                ipAddress);

            return (true, null, customer);
        }

        public async Task<(bool Success, string? Error, Customer? Customer)> UpdateCustomerAsync(Customer customer, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            var existing = await _context.Customers.FindAsync(customer.CustomerId);
            if (existing == null)
            {
                return (false, "Customer not found.", null);
            }

            if (!_crmAccess.CanAccessCustomer(existing, user))
            {
                return (false, "You do not have permission to modify this customer.", null);
            }

            if (string.IsNullOrWhiteSpace(customer.CustomerName))
            {
                return (false, "Customer name is required.", null);
            }

            if (string.IsNullOrWhiteSpace(customer.Email) || !_duplicateDetection.IsValidEmail(customer.Email))
            {
                return (false, "Enter a valid email address.", null);
            }

            if (string.IsNullOrWhiteSpace(customer.Phone) || !_duplicateDetection.IsValidPhone(customer.Phone))
            {
                return (false, "Enter a valid 10-digit Indian mobile number (e.g., 9876543210).", null);
            }

            var normalizedEmail = _duplicateDetection.NormalizeEmail(customer.Email);
            var normalizedPhone = _duplicateDetection.NormalizePhone(customer.Phone);

            if (await EmailExistsAsync(normalizedEmail, existing.CustomerId))
            {
                return (false, "This email address is already registered to another customer.", null);
            }

            if (await PhoneExistsAsync(normalizedPhone, existing.CustomerId))
            {
                return (false, "This phone number is already registered to another customer.", null);
            }

            var oldValue = $"Name={existing.CustomerName}, Email={existing.Email}, Phone={existing.Phone}, Status={existing.Status}";

            existing.CustomerName = customer.CustomerName.Trim();
            existing.Email = normalizedEmail;
            existing.Phone = normalizedPhone;
            existing.CompanyName = customer.CompanyName?.Trim();
            existing.Address = customer.Address?.Trim();
            existing.City = customer.City?.Trim();
            existing.State = customer.State?.Trim();
            existing.Status = customer.Status;
            existing.Notes = customer.Notes?.Trim();
            existing.ModifiedDate = DateTime.UtcNow;
            existing.ModifiedBy = user.Identity?.Name ?? "User";

            // Allow reassignment only by Admin or Manager
            if ((_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)) && !string.IsNullOrEmpty(customer.OwnerId))
            {
                existing.OwnerId = customer.OwnerId;
            }

            var newValue = $"Name={existing.CustomerName}, Email={existing.Email}, Phone={existing.Phone}, Status={existing.Status}";

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Update",
                "Customer",
                existing.CustomerId.ToString(),
                oldValue,
                newValue,
                $"Updated customer {existing.CustomerName}",
                ipAddress);

            return (true, null, existing);
        }

        public async Task<(bool Success, string? Error)> DeleteCustomerAsync(int customerId, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var existing = await _context.Customers.FindAsync(customerId);
            if (existing == null)
            {
                return (false, "Customer not found.");
            }

            if (!_crmAccess.CanAccessCustomer(existing, user))
            {
                return (false, "You do not have permission to delete this customer.");
            }

            // If customer has opportunities or activities, soft deactivate to preserve historical integrity
            var hasDependencies = await _context.Opportunities.AnyAsync(o => o.CustomerId == customerId);
            if (hasDependencies)
            {
                existing.Status = CustomerStatus.Inactive;
                existing.ModifiedDate = DateTime.UtcNow;
                existing.ModifiedBy = user.Identity?.Name ?? "User";
                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    userId,
                    "Deactivate",
                    "Customer",
                    customerId.ToString(),
                    "Status=Active",
                    "Status=Inactive",
                    "Customer deactivated due to existing historical dependencies",
                    ipAddress);

                return (true, null);
            }

            var oldSummary = $"Code={existing.CustomerCode}, Name={existing.CustomerName}";
            _context.Customers.Remove(existing);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Delete",
                "Customer",
                customerId.ToString(),
                oldSummary,
                null,
                $"Deleted customer {existing.CustomerName}",
                ipAddress);

            return (true, null);
        }

        public async Task<Customer?> GetCustomerByIdAsync(int customerId, ClaimsPrincipal user)
        {
            var customer = await _context.Customers
                .Include(c => c.Owner)
                .Include(c => c.Leads)
                .Include(c => c.Opportunities)
                .Include(c => c.FollowUps)
                .Include(c => c.Activities)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null || !_crmAccess.CanAccessCustomer(customer, user))
            {
                return null;
            }

            return customer;
        }

        public async Task<(List<Customer> Items, int TotalCount)> GetCustomersAsync(
            ClaimsPrincipal user,
            string? search,
            CustomerStatus? status,
            string? ownerId,
            string? sortBy,
            bool descending,
            int page,
            int pageSize)
        {
            var query = _context.Customers
                .Include(c => c.Owner)
                .AsNoTracking();

            query = _crmAccess.FilterCustomers(query, user);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(s) ||
                    c.CustomerCode.ToLower().Contains(s) ||
                    c.Email.ToLower().Contains(s) ||
                    c.Phone.Contains(s) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)));
            }

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(ownerId) && (_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)))
            {
                query = query.Where(c => c.OwnerId == ownerId);
            }

            var totalCount = await query.CountAsync();

            query = (sortBy?.ToLower()) switch
            {
                "name" => descending ? query.OrderByDescending(c => c.CustomerName) : query.OrderBy(c => c.CustomerName),
                "code" => descending ? query.OrderByDescending(c => c.CustomerCode) : query.OrderBy(c => c.CustomerCode),
                "email" => descending ? query.OrderByDescending(c => c.Email) : query.OrderBy(c => c.Email),
                "status" => descending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
                _ => descending ? query.OrderByDescending(c => c.CreatedDate) : query.OrderBy(c => c.CreatedDate)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
