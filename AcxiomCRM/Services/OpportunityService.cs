using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class OpportunityService : IOpportunityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;
        private readonly IAuditService _auditService;

        public OpportunityService(
            ApplicationDbContext context,
            ICrmAccessService crmAccess,
            IAuditService auditService)
        {
            _context = context;
            _crmAccess = crmAccess;
            _auditService = auditService;
        }

        public async Task<(bool Success, string? Error, Opportunity? Opportunity)> CreateOpportunityAsync(
            Opportunity opportunity,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            if (string.IsNullOrWhiteSpace(opportunity.OpportunityName))
            {
                return (false, "Opportunity Name is required.", null);
            }

            // Validation: Customer must exist
            var customer = await _context.Customers.FindAsync(opportunity.CustomerId);
            if (customer == null || !_crmAccess.CanAccessCustomer(customer, user))
            {
                return (false, "Selected Customer does not exist or is inaccessible.", null);
            }

            // Validation: Lead if specified
            if (opportunity.LeadId.HasValue)
            {
                var lead = await _context.Leads.FindAsync(opportunity.LeadId.Value);
                if (lead == null || !_crmAccess.CanAccessLead(lead, user))
                {
                    return (false, "Selected Lead does not exist or is inaccessible.", null);
                }
            }

            // Validation: Amount
            if (opportunity.Amount <= 0)
            {
                return (false, "Opportunity Amount must be greater than 0.", null);
            }

            // Validation: Probability
            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                return (false, "Probability must be between 0 and 100.", null);
            }

            // Validation: Expected Close Date
            if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Expected Close Date cannot be in the past.", null);
            }

            // Status alignment
            if (opportunity.Stage == OpportunityStage.Won)
            {
                opportunity.Status = OpportunityStatus.Won;
                opportunity.Probability = 100;
            }
            else if (opportunity.Stage == OpportunityStage.Lost)
            {
                opportunity.Status = OpportunityStatus.Lost;
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = OpportunityStatus.Open;
            }

            // Assignment
            if (_crmAccess.IsSalesExecutive(user) || string.IsNullOrEmpty(opportunity.AssignedTo))
            {
                opportunity.AssignedTo = userId;
            }

            opportunity.CreatedDate = DateTime.UtcNow;
            opportunity.CreatedBy = user.Identity?.Name ?? "User";

            _context.Opportunities.Add(opportunity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Create",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                null,
                $"Name={opportunity.OpportunityName}, Amount={opportunity.Amount}, Stage={opportunity.Stage}",
                $"Created opportunity {opportunity.OpportunityName}",
                ipAddress);

            return (true, null, opportunity);
        }

        public async Task<(bool Success, string? Error, Opportunity? Opportunity)> UpdateOpportunityAsync(
            Opportunity opportunity,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            var existing = await _context.Opportunities.FindAsync(opportunity.OpportunityId);
            if (existing == null)
            {
                return (false, "Opportunity not found.", null);
            }

            if (!_crmAccess.CanAccessOpportunity(existing, user))
            {
                return (false, "You do not have permission to modify this opportunity.", null);
            }

            if (string.IsNullOrWhiteSpace(opportunity.OpportunityName))
            {
                return (false, "Opportunity Name is required.", null);
            }

            if (opportunity.Amount <= 0)
            {
                return (false, "Opportunity Amount must be greater than 0.", null);
            }

            if (opportunity.Probability < 0 || opportunity.Probability > 100)
            {
                return (false, "Probability must be between 0 and 100.", null);
            }

            // Close date check if still active
            if (opportunity.Stage != OpportunityStage.Won && opportunity.Stage != OpportunityStage.Lost)
            {
                if (opportunity.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
                {
                    return (false, "Expected Close Date cannot be in the past.", null);
                }
            }

            var oldSummary = $"Name={existing.OpportunityName}, Amount={existing.Amount}, Stage={existing.Stage}, Prob={existing.Probability}";

            existing.OpportunityName = opportunity.OpportunityName.Trim();
            existing.Amount = opportunity.Amount;
            existing.Stage = opportunity.Stage;
            existing.Probability = opportunity.Probability;
            existing.ExpectedCloseDate = opportunity.ExpectedCloseDate;
            existing.Notes = opportunity.Notes?.Trim();
            existing.ModifiedDate = DateTime.UtcNow;
            existing.ModifiedBy = user.Identity?.Name ?? "User";

            if (opportunity.Stage == OpportunityStage.Won)
            {
                existing.Status = OpportunityStatus.Won;
                existing.Probability = 100;
            }
            else if (opportunity.Stage == OpportunityStage.Lost)
            {
                existing.Status = OpportunityStatus.Lost;
                existing.Probability = 0;
            }
            else
            {
                existing.Status = OpportunityStatus.Open;
            }

            if ((_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)) && !string.IsNullOrEmpty(opportunity.AssignedTo))
            {
                existing.AssignedTo = opportunity.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var newSummary = $"Name={existing.OpportunityName}, Amount={existing.Amount}, Stage={existing.Stage}, Prob={existing.Probability}";

            await _auditService.LogAsync(
                userId,
                "Update",
                "Opportunity",
                existing.OpportunityId.ToString(),
                oldSummary,
                newSummary,
                $"Updated opportunity {existing.OpportunityName}",
                ipAddress);

            return (true, null, existing);
        }

        public async Task<(bool Success, string? Error)> ChangeStageAsync(
            int opportunityId,
            OpportunityStage newStage,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var opportunity = await _context.Opportunities.FindAsync(opportunityId);
            if (opportunity == null)
            {
                return (false, "Opportunity not found.");
            }

            if (!_crmAccess.CanAccessOpportunity(opportunity, user))
            {
                return (false, "You do not have permission to modify this opportunity.");
            }

            var oldStage = opportunity.Stage;
            opportunity.Stage = newStage;
            opportunity.ModifiedDate = DateTime.UtcNow;
            opportunity.ModifiedBy = user.Identity?.Name ?? "User";

            if (newStage == OpportunityStage.Won)
            {
                opportunity.Status = OpportunityStatus.Won;
                opportunity.Probability = 100;
            }
            else if (newStage == OpportunityStage.Lost)
            {
                opportunity.Status = OpportunityStatus.Lost;
                opportunity.Probability = 0;
            }
            else
            {
                opportunity.Status = OpportunityStatus.Open;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "StageChange",
                "Opportunity",
                opportunity.OpportunityId.ToString(),
                $"Stage={oldStage}",
                $"Stage={newStage}, Status={opportunity.Status}",
                $"Changed stage of opportunity {opportunity.OpportunityName} to {newStage}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteOpportunityAsync(
            int opportunityId,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var opp = await _context.Opportunities.FindAsync(opportunityId);
            if (opp == null)
            {
                return (false, "Opportunity not found.");
            }

            if (!_crmAccess.CanAccessOpportunity(opp, user))
            {
                return (false, "You do not have permission to delete this opportunity.");
            }

            var summary = $"Id={opp.OpportunityId}, Name={opp.OpportunityName}, Amount={opp.Amount}";
            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Delete",
                "Opportunity",
                opportunityId.ToString(),
                summary,
                null,
                $"Deleted opportunity {opp.OpportunityName}",
                ipAddress);

            return (true, null);
        }

        public async Task<Opportunity?> GetOpportunityByIdAsync(int opportunityId, ClaimsPrincipal user)
        {
            var opp = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .Include(o => o.FollowUps)
                .Include(o => o.Activities)
                .FirstOrDefaultAsync(o => o.OpportunityId == opportunityId);

            if (opp == null || !_crmAccess.CanAccessOpportunity(opp, user))
            {
                return null;
            }

            return opp;
        }

        public async Task<(List<Opportunity> Items, int TotalCount)> GetOpportunitiesAsync(
            ClaimsPrincipal user,
            string? search,
            OpportunityStage? stage,
            OpportunityStatus? status,
            string? assignedTo,
            int? customerId,
            string? sortBy,
            bool descending,
            int page,
            int pageSize)
        {
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedUser)
                .AsNoTracking();

            query = _crmAccess.FilterOpportunities(query, user);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(o =>
                    o.OpportunityName.ToLower().Contains(s) ||
                    (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(s)));
            }

            if (stage.HasValue)
            {
                query = query.Where(o => o.Stage == stage.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(o => o.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo) && (_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)))
            {
                query = query.Where(o => o.AssignedTo == assignedTo);
            }

            var totalCount = await query.CountAsync();

            query = (sortBy?.ToLower()) switch
            {
                "name" => descending ? query.OrderByDescending(o => o.OpportunityName) : query.OrderBy(o => o.OpportunityName),
                "amount" => descending ? query.OrderByDescending(o => o.Amount) : query.OrderBy(o => o.Amount),
                "probability" => descending ? query.OrderByDescending(o => o.Probability) : query.OrderBy(o => o.Probability),
                "stage" => descending ? query.OrderByDescending(o => o.Stage) : query.OrderBy(o => o.Stage),
                "date" => descending ? query.OrderByDescending(o => o.ExpectedCloseDate) : query.OrderBy(o => o.ExpectedCloseDate),
                _ => descending ? query.OrderByDescending(o => o.CreatedDate) : query.OrderBy(o => o.CreatedDate)
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(decimal TotalPipeline, decimal WeightedPipeline, int OpenDeals, int WonDeals, int LostDeals)> GetPipelineSummaryAsync(ClaimsPrincipal user)
        {
            var query = _context.Opportunities.AsNoTracking();
            query = _crmAccess.FilterOpportunities(query, user);

            var openQuery = query.Where(o => o.Status == OpportunityStatus.Open);

            var totalPipeline = await openQuery.SumAsync(o => (decimal?)o.Amount) ?? 0m;
            var weightedPipeline = await openQuery.SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;
            var openDeals = await openQuery.CountAsync();
            var wonDeals = await query.CountAsync(o => o.Status == OpportunityStatus.Won);
            var lostDeals = await query.CountAsync(o => o.Status == OpportunityStatus.Lost);

            return (totalPipeline, weightedPipeline, openDeals, wonDeals, lostDeals);
        }
    }
}
