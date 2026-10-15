using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class CrmAccessService : ICrmAccessService
    {
        public string? GetCurrentUserId(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public bool IsAdmin(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Admin);
        }

        public bool IsManager(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.Manager);
        }

        public bool IsSalesExecutive(ClaimsPrincipal user)
        {
            return user.IsInRole(AppRoles.SalesExecutive);
        }

        public bool CanAccessCustomer(Customer customer, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetCurrentUserId(user);
            return !string.IsNullOrEmpty(userId) && customer.OwnerId == userId;
        }

        public bool CanAccessLead(Lead lead, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetCurrentUserId(user);
            return !string.IsNullOrEmpty(userId) && lead.AssignedTo == userId;
        }

        public bool CanAccessOpportunity(Opportunity opportunity, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetCurrentUserId(user);
            return !string.IsNullOrEmpty(userId) && opportunity.AssignedTo == userId;
        }

        public bool CanAccessFollowUp(FollowUp followUp, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetCurrentUserId(user);
            return !string.IsNullOrEmpty(userId) && followUp.AssignedTo == userId;
        }

        public bool CanAccessActivity(Activity activity, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return true;
            var userId = GetCurrentUserId(user);
            return !string.IsNullOrEmpty(userId) && activity.AssignedTo == userId;
        }

        public IQueryable<Customer> FilterCustomers(IQueryable<Customer> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetCurrentUserId(user);
            return query.Where(c => c.OwnerId == userId);
        }

        public IQueryable<Lead> FilterLeads(IQueryable<Lead> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetCurrentUserId(user);
            return query.Where(l => l.AssignedTo == userId);
        }

        public IQueryable<Opportunity> FilterOpportunities(IQueryable<Opportunity> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetCurrentUserId(user);
            return query.Where(o => o.AssignedTo == userId);
        }

        public IQueryable<FollowUp> FilterFollowUps(IQueryable<FollowUp> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetCurrentUserId(user);
            return query.Where(f => f.AssignedTo == userId);
        }

        public IQueryable<Activity> FilterActivities(IQueryable<Activity> query, ClaimsPrincipal user)
        {
            if (IsAdmin(user) || IsManager(user)) return query;
            var userId = GetCurrentUserId(user);
            return query.Where(a => a.AssignedTo == userId);
        }
    }
}
