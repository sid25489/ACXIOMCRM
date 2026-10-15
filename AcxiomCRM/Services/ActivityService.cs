using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class ActivityService : IActivityService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;
        private readonly IAuditService _auditService;

        public ActivityService(
            ApplicationDbContext context,
            ICrmAccessService crmAccess,
            IAuditService auditService)
        {
            _context = context;
            _crmAccess = crmAccess;
            _auditService = auditService;
        }

        public async Task<(bool Success, string? Error, Activity? Activity)> CreateActivityAsync(
            Activity activity,
            ClaimsPrincipal user,
            string? ipAddress = null)
        {
            var userId = _crmAccess.GetCurrentUserId(user);
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Unauthorized user context.", null);
            }

            if (string.IsNullOrWhiteSpace(activity.Subject))
            {
                return (false, "Subject is required.", null);
            }

            if (!activity.CustomerId.HasValue && !activity.LeadId.HasValue && !activity.OpportunityId.HasValue)
            {
                return (false, "An activity must be linked to at least one Customer, Lead, or Opportunity.", null);
            }

            if (activity.CustomerId.HasValue)
            {
                var cust = await _context.Customers.FindAsync(activity.CustomerId.Value);
                if (cust == null || !_crmAccess.CanAccessCustomer(cust, user))
                {
                    return (false, "Selected Customer is invalid or inaccessible.", null);
                }
            }

            if (activity.LeadId.HasValue)
            {
                var lead = await _context.Leads.FindAsync(activity.LeadId.Value);
                if (lead == null || !_crmAccess.CanAccessLead(lead, user))
                {
                    return (false, "Selected Lead is invalid or inaccessible.", null);
                }
            }

            if (activity.OpportunityId.HasValue)
            {
                var opp = await _context.Opportunities.FindAsync(activity.OpportunityId.Value);
                if (opp == null || !_crmAccess.CanAccessOpportunity(opp, user))
                {
                    return (false, "Selected Opportunity is invalid or inaccessible.", null);
                }
            }

            if (_crmAccess.IsSalesExecutive(user) || string.IsNullOrEmpty(activity.AssignedTo))
            {
                activity.AssignedTo = userId;
            }

            activity.CreatedDate = DateTime.UtcNow;
            activity.CreatedBy = user.Identity?.Name ?? "User";

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                userId,
                "Create",
                "Activity",
                activity.ActivityId.ToString(),
                null,
                $"Type={activity.ActivityType}, Subject={activity.Subject}",
                $"Logged activity: {activity.Subject}",
                ipAddress);

            return (true, null, activity);
        }

        public async Task<Activity?> GetActivityByIdAsync(int activityId, ClaimsPrincipal user)
        {
            var activity = await _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.Opportunity)
                .Include(a => a.AssignedUser)
                .FirstOrDefaultAsync(a => a.ActivityId == activityId);

            if (activity == null || !_crmAccess.CanAccessActivity(activity, user))
            {
                return null;
            }

            return activity;
        }

        public async Task<(List<Activity> Items, int TotalCount)> GetActivitiesAsync(
            ClaimsPrincipal user,
            ActivityType? type,
            ActivityStatus? status,
            string? assignedTo,
            int? customerId,
            int? leadId,
            int? opportunityId,
            int page,
            int pageSize)
        {
            var query = _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.Opportunity)
                .Include(a => a.AssignedUser)
                .AsNoTracking();

            query = _crmAccess.FilterActivities(query, user);

            if (type.HasValue)
            {
                query = query.Where(a => a.ActivityType == type.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            if (!string.IsNullOrWhiteSpace(assignedTo) && (_crmAccess.IsAdmin(user) || _crmAccess.IsManager(user)))
            {
                query = query.Where(a => a.AssignedTo == assignedTo);
            }

            if (customerId.HasValue)
            {
                query = query.Where(a => a.CustomerId == customerId.Value);
            }

            if (leadId.HasValue)
            {
                query = query.Where(a => a.LeadId == leadId.Value);
            }

            if (opportunityId.HasValue)
            {
                query = query.Where(a => a.OpportunityId == opportunityId.Value);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.ActivityDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
