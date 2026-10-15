using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class FollowUpService : IFollowUpService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;
        private readonly IAuditService _auditService;

        public FollowUpService(
            ApplicationDbContext context,
            ICrmAccessService crmAccess,
            IAuditService auditService)
        {
            _context = context;
            _crmAccess = crmAccess;
            _auditService = auditService;
        }

        public async Task<(bool Success, string? Error, FollowUp? FollowUp)> CreateFollowUpAsync(
            FollowUp followUp,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            if (string.IsNullOrWhiteSpace(followUp.Subject))
            {
                return (false, "Subject is required.", null);
            }

            // Date validation: Follow-up date cannot be earlier than today for planned
            if (followUp.Status == FollowUpStatus.Planned && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Follow-up date cannot be earlier than today.", null);
            }

            // Related record validation: At least one must be set
            if (!followUp.CustomerId.HasValue && !followUp.LeadId.HasValue && !followUp.OpportunityId.HasValue)
            {
                return (false, "A follow-up must be associated with at least one Customer, Lead, or Opportunity.", null);
            }

            if (followUp.CustomerId.HasValue)
            {
                var cust = await _context.Customers.FindAsync(followUp.CustomerId.Value);
                if (cust == null || !_crmAccess.CanAccessCustomer(cust, user))
                {
                    return (false, "Selected Customer does not exist or is inaccessible.", null);
                }
            }

            if (followUp.LeadId.HasValue)
            {
                var lead = await _context.Leads.FindAsync(followUp.LeadId.Value);
                if (lead == null || !_crmAccess.CanAccessLead(lead, user))
                {
                    return (false, "Selected Lead does not exist or is inaccessible.", null);
                }
            }

            if (followUp.OpportunityId.HasValue)
            {
                var opp = await _context.Opportunities.FindAsync(followUp.OpportunityId.Value);
                if (opp == null || !_crmAccess.CanAccessOpportunity(opp, user))
                {
                    return (false, "Selected Opportunity does not exist or is inaccessible.", null);
                }
            }

            if (_crmAccess.IsSalesExecutive(user) || string.IsNullOrEmpty(followUp.AssignedTo))
            {
                followUp.AssignedTo = userId;
            }

            followUp.CreatedDate = DateTime.UtcNow;
            followUp.CreatedBy = user.Identity?.Name ?? "User";

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                null,
                $"Subject={followUp.Subject}, Date={followUp.FollowUpDate:yyyy-MM-dd}, Type={followUp.FollowUpType}",
                $"Scheduled follow-up: {followUp.Subject}",
                ipAddress);

            return (true, null, followUp);
        }

        public async Task<(bool Success, string? Error, FollowUp? FollowUp)> UpdateFollowUpAsync(
            FollowUp followUp,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            var existing = await _context.FollowUps.FindAsync(followUp.FollowUpId);
            if (existing == null)
            {
                return (false, "Follow-up not found.", null);
            }

            if (!_crmAccess.CanAccessFollowUp(existing, user))
            {
                return (false, "You do not have permission to modify this follow-up.", null);
            }

            if (string.IsNullOrWhiteSpace(followUp.Subject))
            {
                return (false, "Subject is required.", null);
            }

            var oldSummary = $"Subject={existing.Subject}, Date={existing.FollowUpDate:yyyy-MM-dd}, Status={existing.Status}";

            existing.Subject = followUp.Subject.Trim();
            existing.FollowUpType = followUp.FollowUpType;
            existing.FollowUpDate = followUp.FollowUpDate;
            existing.Remarks = followUp.Remarks?.Trim();
            existing.Status = followUp.Status;

            if (existing.Status == FollowUpStatus.Completed && !existing.CompletedDate.HasValue)
            {
                existing.CompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var newSummary = $"Subject={existing.Subject}, Date={existing.FollowUpDate:yyyy-MM-dd}, Status={existing.Status}";

            await _auditService.LogAsync(
                userId,
                "Update",
                "FollowUp",
                existing.FollowUpId.ToString(),
                oldSummary,
                newSummary,
                $"Updated follow-up: {existing.Subject}",
                ipAddress);

            return (true, null, existing);
        }

        public async Task<(bool Success, string? Error)> CompleteFollowUpAsync(
            int followUpId,
            string? remarks,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var followUp = await _context.FollowUps.FindAsync(followUpId);
            if (followUp == null)
            {
                return (false, "Follow-up not found.");
            }

            if (!_crmAccess.CanAccessFollowUp(followUp, user))
            {
                return (false, "You do not have permission to modify this follow-up.");
            }

            var oldStatus = followUp.Status;
            followUp.Status = FollowUpStatus.Completed;
            followUp.CompletedDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                followUp.Remarks = string.IsNullOrWhiteSpace(followUp.Remarks)
                    ? remarks
                    : $"{followUp.Remarks}\n[Completion Note]: {remarks}";
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Complete",
                "FollowUp",
                followUpId.ToString(),
                $"Status={oldStatus}",
                "Status=Completed",
                $"Completed follow-up: {followUp.Subject}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> RescheduleFollowUpAsync(
            int followUpId,
            DateTime newDate,
            string? remarks,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var followUp = await _context.FollowUps.FindAsync(followUpId);
            if (followUp == null)
            {
                return (false, "Follow-up not found.");
            }

            if (!_crmAccess.CanAccessFollowUp(followUp, user))
            {
                return (false, "You do not have permission to reschedule this follow-up.");
            }

            if (newDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Follow-up date cannot be earlier than today.");
            }

            var oldDate = followUp.FollowUpDate;
            followUp.FollowUpDate = newDate;
            followUp.Status = FollowUpStatus.Planned;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                followUp.Remarks = string.IsNullOrWhiteSpace(followUp.Remarks)
                    ? $"[Rescheduled]: {remarks}"
                    : $"{followUp.Remarks}\n[Rescheduled]: {remarks}";
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Reschedule",
                "FollowUp",
                followUpId.ToString(),
                $"Date={oldDate:yyyy-MM-dd}",
                $"Date={newDate:yyyy-MM-dd}",
                $"Rescheduled follow-up '{followUp.Subject}' to {newDate:yyyy-MM-dd}",
                ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> CancelFollowUpAsync(
            int followUpId,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            var followUp = await _context.FollowUps.FindAsync(followUpId);
            if (followUp == null)
            {
                return (false, "Follow-up not found.");
            }

            if (!_crmAccess.CanAccessFollowUp(followUp, user))
            {
                return (false, "You do not have permission to cancel this follow-up.");
            }

            var oldStatus = followUp.Status;
            followUp.Status = FollowUpStatus.Cancelled;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Cancel",
                "FollowUp",
                followUpId.ToString(),
                $"Status={oldStatus}",
                "Status=Cancelled",
                $"Cancelled follow-up: {followUp.Subject}",
                ipAddress);

            return (true, null);
        }

        public async Task<FollowUp?> GetFollowUpByIdAsync(int followUpId, ClaimsPrincipal user)
        {
            var followUp = await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedUser)
                .FirstOrDefaultAsync(f => f.FollowUpId == followUpId);

            if (followUp == null || !_crmAccess.CanAccessFollowUp(followUp, user))
            {
                return null;
            }

            return followUp;
        }

        public async Task<(List<FollowUp> Items, int TotalCount)> GetFollowUpsAsync(
            ClaimsPrincipal user,
            FollowUpStatus? status,
            FollowUpType? type,
            string? assignedTo,
            DateTime? fromDate,
            DateTime? toDate,
            int page,
            int pageSize)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedUser)
                .AsNoTracking();

            query = _crmAccess.FilterFollowUps(query, user);

            if (status.HasValue)
            {
                query = query.Where(f => f.Status == status.Value);
            }

            if (type.HasValue)
            {
                query = query.Where(f => f.FollowUpType == type.Value);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo) && (_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)))
            {
                query = query.Where(f => f.AssignedTo == assignedTo);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(f => f.FollowUpDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(f => f.FollowUpDate <= end);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(f => f.FollowUpDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<FollowUp>> GetUpcomingFollowUpsAsync(ClaimsPrincipal user, int days = 7)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .AsNoTracking();

            query = _crmAccess.FilterFollowUps(query, user);

            var today = DateTime.UtcNow.Date;
            var endDate = today.AddDays(days).AddDays(1).AddTicks(-1);

            return await query
                .Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate >= today && f.FollowUpDate <= endDate)
                .OrderBy(f => f.FollowUpDate)
                .ToListAsync();
        }

        public async Task<List<FollowUp>> GetOverdueFollowUpsAsync(ClaimsPrincipal user)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .AsNoTracking();

            query = _crmAccess.FilterFollowUps(query, user);

            var today = DateTime.UtcNow.Date;

            return await query
                .Where(f => f.Status == FollowUpStatus.Planned && f.FollowUpDate.Date < today)
                .OrderBy(f => f.FollowUpDate)
                .ToListAsync();
        }
    }
}
