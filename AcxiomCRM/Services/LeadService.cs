using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class LeadService : ILeadService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;
        private readonly IAuditService _auditService;
        private readonly IDuplicateDetectionService _duplicateDetection;

        public LeadService(
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

        public bool IsValidStatusTransition(LeadStatus currentStatus, LeadStatus targetStatus, bool isAdminOrManager)
        {
            if (currentStatus == targetStatus) return true;

            // Converted lead cannot be changed
            if (currentStatus == LeadStatus.Converted) return false;

            return currentStatus switch
            {
                LeadStatus.New => targetStatus is LeadStatus.Contacted or LeadStatus.Lost,
                LeadStatus.Contacted => targetStatus is LeadStatus.Qualified or LeadStatus.Unqualified or LeadStatus.Lost,
                LeadStatus.Qualified => targetStatus is LeadStatus.Converted or LeadStatus.Lost or LeadStatus.Contacted,
                LeadStatus.Unqualified => isAdminOrManager && targetStatus is LeadStatus.Contacted or LeadStatus.New,
                LeadStatus.Lost => isAdminOrManager && targetStatus is LeadStatus.Contacted or LeadStatus.New,
                _ => false
            };
        }

        public async Task<(bool Success, string? Error, Lead? Lead)> CreateLeadAsync(Lead lead, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            if (string.IsNullOrWhiteSpace(lead.LeadName))
            {
                return (false, "Lead name is required.", null);
            }

            if (!string.IsNullOrWhiteSpace(lead.Email) && !_duplicateDetection.IsValidEmail(lead.Email))
            {
                return (false, "Enter a valid email address.", null);
            }

            if (!string.IsNullOrWhiteSpace(lead.Phone) && !_duplicateDetection.IsValidPhone(lead.Phone))
            {
                return (false, "Enter a valid 10-digit Indian mobile number (e.g., 9876543210).", null);
            }

            if (lead.ExpectedValue < 0)
            {
                return (false, "Expected value cannot be negative.", null);
            }

            if (!string.IsNullOrWhiteSpace(lead.Email))
            {
                lead.Email = _duplicateDetection.NormalizeEmail(lead.Email);
            }

            if (!string.IsNullOrWhiteSpace(lead.Phone))
            {
                lead.Phone = _duplicateDetection.NormalizePhone(lead.Phone);
            }

            // Assign user
            if (_crmAccess.IsSalesExecutive(user) || string.IsNullOrEmpty(lead.AssignedTo))
            {
                lead.AssignedTo = userId;
            }

            if (string.IsNullOrWhiteSpace(lead.LeadCode))
            {
                var maxId = await _context.Leads.Select(l => (int?)l.LeadId).MaxAsync() ?? 0;
                lead.LeadCode = $"LEAD-{(maxId + 1):D5}";
            }

            lead.CreatedDate = DateTime.UtcNow;
            lead.CreatedBy = user.Identity?.Name ?? "User";

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Create",
                "Lead",
                lead.LeadId.ToString(),
                null,
                $"Code={lead.LeadCode}, Name={lead.LeadName}, Status={lead.Status}, Value={lead.ExpectedValue}",
                $"Created lead {lead.LeadName}",
                ipAddress);

            return (true, null, lead);
        }

        public async Task<(bool Success, string? Error, Lead? Lead)> UpdateLeadAsync(Lead lead, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            var existing = await _context.Leads.FindAsync(lead.LeadId);
            if (existing == null)
            {
                return (false, "Lead not found.", null);
            }

            if (!_crmAccess.CanAccessLead(existing, user))
            {
                return (false, "You do not have permission to modify this lead.", null);
            }

            if (existing.Status == LeadStatus.Converted && lead.Status != LeadStatus.Converted)
            {
                return (false, "A converted lead cannot have its status changed.", null);
            }

            var isAdminOrManager = _crmAccess.IsAdmin(user) || _crmAccess.IsManager(user);
            if (!IsValidStatusTransition(existing.Status, lead.Status, isAdminOrManager))
            {
                return (false, $"Invalid lead status transition from {existing.Status} to {lead.Status}.", null);
            }

            if (string.IsNullOrWhiteSpace(lead.LeadName))
            {
                return (false, "Lead name is required.", null);
            }

            if (!string.IsNullOrWhiteSpace(lead.Email) && !_duplicateDetection.IsValidEmail(lead.Email))
            {
                return (false, "Enter a valid email address.", null);
            }

            if (!string.IsNullOrWhiteSpace(lead.Phone) && !_duplicateDetection.IsValidPhone(lead.Phone))
            {
                return (false, "Enter a valid 10-digit Indian mobile number (e.g., 9876543210).", null);
            }

            if (lead.ExpectedValue < 0)
            {
                return (false, "Expected value cannot be negative.", null);
            }

            var oldSummary = $"Name={existing.LeadName}, Status={existing.Status}, Value={existing.ExpectedValue}";

            existing.LeadName = lead.LeadName.Trim();
            existing.Email = string.IsNullOrWhiteSpace(lead.Email) ? null : _duplicateDetection.NormalizeEmail(lead.Email);
            existing.Phone = string.IsNullOrWhiteSpace(lead.Phone) ? null : _duplicateDetection.NormalizePhone(lead.Phone);
            existing.CompanyName = lead.CompanyName?.Trim();
            existing.Source = lead.Source;
            existing.Status = lead.Status;
            existing.Priority = lead.Priority;
            existing.ExpectedValue = lead.ExpectedValue;
            existing.Notes = lead.Notes?.Trim();
            existing.ModifiedDate = DateTime.UtcNow;
            existing.ModifiedBy = user.Identity?.Name ?? "User";

            if (isAdminOrManager && !string.IsNullOrEmpty(lead.AssignedTo))
            {
                existing.AssignedTo = lead.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var newSummary = $"Name={existing.LeadName}, Status={existing.Status}, Value={existing.ExpectedValue}";

            await _auditService.LogAsync(
                userId,
                "Update",
                "Lead",
                existing.LeadId.ToString(),
                oldSummary,
                newSummary,
                $"Updated lead {existing.LeadName}",
                ipAddress);

            return (true, null, existing);
        }

        public async Task<(bool Success, string? Error)> ChangeStatusAsync(int leadId, LeadStatus newStatus, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null)
            {
                return (false, "Lead not found.");
            }

            if (!_crmAccess.CanAccessLead(lead, user))
            {
                return (false, "You do not have permission to modify this lead.");
            }

            var isAdminOrManager = _crmAccess.IsAdmin(user) || _crmAccess.IsManager(user);
            if (!IsValidStatusTransition(lead.Status, newStatus, isAdminOrManager))
            {
                return (false, $"Invalid status transition from {lead.Status} to {newStatus}.");
            }

            var oldStatus = lead.Status;
            lead.Status = newStatus;
            lead.ModifiedDate = DateTime.UtcNow;
            lead.ModifiedBy = user.Identity?.Name ?? "User";

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "StatusChange",
                "Lead",
                lead.LeadId.ToString(),
                $"Status={oldStatus}",
                $"Status={newStatus}",
                $"Changed status of lead {lead.LeadName} from {oldStatus} to {newStatus}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error, Customer? Customer, Opportunity? Opportunity)> ConvertLeadAsync(
            ConvertLeadModel model,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var lead = await _context.Leads
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(l => l.LeadId == model.LeadId);

            if (lead == null)
            {
                return (false, "Lead not found.", null, null);
            }

            if (!_crmAccess.CanAccessLead(lead, user))
            {
                return (false, "You do not have permission to convert this lead.", null, null);
            }

            if (lead.Status == LeadStatus.Converted)
            {
                return (false, "This lead has already been converted.", null, null);
            }

            // Lead must be Qualified to be converted
            if (lead.Status != LeadStatus.Qualified)
            {
                return (false, $"Lead must be in 'Qualified' status to be converted. Current status is {lead.Status}.", null, null);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                Customer customer;

                // 1. Check if customer already linked or matching customer exists
                if (lead.CustomerId.HasValue)
                {
                    customer = await _context.Customers.FirstAsync(c => c.CustomerId == lead.CustomerId.Value);
                }
                else
                {
                    // Check by email or phone
                    var normalizedEmail = string.IsNullOrWhiteSpace(lead.Email) ? null : _duplicateDetection.NormalizeEmail(lead.Email);
                    var normalizedPhone = string.IsNullOrWhiteSpace(lead.Phone) ? null : _duplicateDetection.NormalizePhone(lead.Phone);

                    var matchedCustomer = await _context.Customers.FirstOrDefaultAsync(c =>
                        (!string.IsNullOrEmpty(normalizedEmail) && c.Email.ToLower() == normalizedEmail) ||
                        (!string.IsNullOrEmpty(normalizedPhone) && c.Phone == normalizedPhone));

                    if (matchedCustomer != null)
                    {
                        customer = matchedCustomer;
                    }
                    else
                    {
                        // Create customer from lead data
                        var maxCustId = await _context.Customers.Select(c => (int?)c.CustomerId).MaxAsync() ?? 0;
                        var fallbackEmail = !string.IsNullOrWhiteSpace(lead.Email) ? lead.Email : $"customer_{maxCustId + 1}@lead-conversion.local";
                        var fallbackPhone = !string.IsNullOrWhiteSpace(lead.Phone) ? lead.Phone : $"99{DateTime.UtcNow.Ticks % 100000000:D8}";

                        customer = new Customer
                        {
                            CustomerCode = $"CUST-{(maxCustId + 1):D5}",
                            CustomerName = lead.CompanyName ?? lead.LeadName,
                            Email = _duplicateDetection.NormalizeEmail(fallbackEmail),
                            Phone = _duplicateDetection.NormalizePhone(fallbackPhone),
                            CompanyName = lead.CompanyName,
                            Status = CustomerStatus.Active,
                            OwnerId = lead.AssignedTo,
                            CreatedBy = user.Identity?.Name ?? "LeadConversion",
                            CreatedDate = DateTime.UtcNow,
                            Notes = $"Converted from Lead [{lead.LeadCode}]: {lead.LeadName}. {lead.Notes}"
                        };

                        _context.Customers.Add(customer);
                        await _context.SaveChangesAsync();

                        await _auditService.LogAsync(
                            userId,
                            "Create",
                            "Customer",
                            customer.CustomerId.ToString(),
                            null,
                            $"Created from Lead {lead.LeadCode}",
                            $"Customer created during lead conversion: {customer.CustomerName}",
                            ipAddress);
                    }
                }

                Opportunity? opportunity = null;
                if (model.CreateOpportunity)
                {
                    var oppAmount = model.OpportunityAmount ?? (lead.ExpectedValue > 0 ? lead.ExpectedValue : 50000m);
                    var oppCloseDate = model.ExpectedCloseDate ?? DateTime.UtcNow.AddDays(30);

                    if (oppAmount <= 0)
                    {
                        await transaction.RollbackAsync();
                        return (false, "Opportunity Amount must be greater than 0.", null, null);
                    }

                    if (oppCloseDate.Date < DateTime.UtcNow.Date)
                    {
                        await transaction.RollbackAsync();
                        return (false, "Expected Close Date cannot be in the past.", null, null);
                    }

                    opportunity = new Opportunity
                    {
                        OpportunityName = string.IsNullOrWhiteSpace(model.OpportunityName) ? $"{lead.LeadName} - Opportunity" : model.OpportunityName.Trim(),
                        CustomerId = customer.CustomerId,
                        LeadId = lead.LeadId,
                        Amount = oppAmount,
                        Stage = OpportunityStage.Qualification,
                        Probability = 25,
                        ExpectedCloseDate = oppCloseDate,
                        Status = OpportunityStatus.Open,
                        AssignedTo = lead.AssignedTo,
                        CreatedBy = user.Identity?.Name ?? "LeadConversion",
                        CreatedDate = DateTime.UtcNow,
                        Notes = $"Created during conversion of lead [{lead.LeadCode}]."
                    };

                    _context.Opportunities.Add(opportunity);
                    await _context.SaveChangesAsync();

                    await _auditService.LogAsync(
                        userId,
                        "Create",
                        "Opportunity",
                        opportunity.OpportunityId.ToString(),
                        null,
                        $"Name={opportunity.OpportunityName}, Amount={opportunity.Amount}",
                        $"Opportunity generated from lead conversion",
                        ipAddress);
                }

                // Update lead
                lead.Status = LeadStatus.Converted;
                lead.CustomerId = customer.CustomerId;
                lead.ConversionDate = DateTime.UtcNow;
                lead.ModifiedDate = DateTime.UtcNow;
                lead.ModifiedBy = user.Identity?.Name ?? "LeadConversion";

                await _context.SaveChangesAsync();

                await _auditService.LogAsync(
                    userId,
                    "ConvertLead",
                    "Lead",
                    lead.LeadId.ToString(),
                    "Status=Qualified",
                    $"Status=Converted, CustomerId={customer.CustomerId}, OppId={opportunity?.OpportunityId}",
                    $"Converted lead {lead.LeadName} into Customer {customer.CustomerName}",
                    ipAddress);

                await transaction.CommitAsync();

                return (true, null, customer, opportunity);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Failed to convert lead: {ex.Message}", null, null);
            }
        }

        public async Task<(bool Success, string? Error)> DeleteLeadAsync(int leadId, ClaimsPrincipal user, string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var lead = await _context.Leads.FindAsync(leadId);
            if (lead == null)
            {
                return (false, "Lead not found.");
            }

            if (!_crmAccess.CanAccessLead(lead, user))
            {
                return (false, "You do not have permission to delete this lead.");
            }

            if (lead.Status == LeadStatus.Converted)
            {
                return (false, "A converted lead cannot be deleted as it forms an audit trail.");
            }

            var summary = $"Code={lead.LeadCode}, Name={lead.LeadName}";
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Delete",
                "Lead",
                leadId.ToString(),
                summary,
                null,
                $"Deleted lead {lead.LeadName}",
                ipAddress);

            return (true, null);
        }

        public async Task<Lead?> GetLeadByIdAsync(int leadId, ClaimsPrincipal user)
        {
            var lead = await _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.Customer)
                .Include(l => l.Opportunities)
                .Include(l => l.FollowUps)
                .Include(l => l.Activities)
                .FirstOrDefaultAsync(l => l.LeadId == leadId);

            if (lead == null || !_crmAccess.CanAccessLead(lead, user))
            {
                return null;
            }

            return lead;
        }

        public async Task<(List<Lead> Items, int TotalCount)> GetLeadsAsync(
            ClaimsPrincipal user,
            string? search,
            LeadStatus? status,
            LeadSource? source,
            string? assignedTo,
            string? sortBy,
            bool descending,
            int page,
            int pageSize)
        {
            var query = _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.Customer)
                .AsNoTracking();

            query = _crmAccess.FilterLeads(query, user);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(l =>
                    l.LeadName.ToLower().Contains(s) ||
                    l.LeadCode.ToLower().Contains(s) ||
                    (l.Email != null && l.Email.ToLower().Contains(s)) ||
                    (l.Phone != null && l.Phone.Contains(s)) ||
                    (l.CompanyName != null && l.CompanyName.ToLower().Contains(s)));
            }

            if (status.HasValue)
            {
                query = query.Where(l => l.Status == status.Value);
            }

            if (source.HasValue)
            {
                query = query.Where(l => l.Source == source.Value);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo) && (_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)))
            {
                query = query.Where(l => l.AssignedTo == assignedTo);
            }

            var totalCount = await query.CountAsync();

            query = (sortBy?.ToLower()) switch
            {
                "name" => descending ? query.OrderByDescending(l => l.LeadName) : query.OrderBy(l => l.LeadName),
                "code" => descending ? query.OrderByDescending(l => l.LeadCode) : query.OrderBy(l => l.LeadCode),
                "status" => descending ? query.OrderByDescending(l => l.Status) : query.OrderBy(l => l.Status),
                "value" => descending ? query.OrderByDescending(l => l.ExpectedValue) : query.OrderBy(l => l.ExpectedValue),
                _ => descending ? query.OrderByDescending(l => l.CreatedDate) : query.OrderBy(l => l.CreatedDate)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
