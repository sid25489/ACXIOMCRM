using AcxiomCRM.Models;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public interface ICrmAccessService
    {
        string? GetCurrentUserId(ClaimsPrincipal user);
        bool IsAdmin(ClaimsPrincipal user);
        bool IsManager(ClaimsPrincipal user);
        bool IsSalesExecutive(ClaimsPrincipal user);

        bool CanAccessCustomer(Customer customer, ClaimsPrincipal user);
        bool CanAccessLead(Lead lead, ClaimsPrincipal user);
        bool CanAccessOpportunity(Opportunity opportunity, ClaimsPrincipal user);
        bool CanAccessFollowUp(FollowUp followUp, ClaimsPrincipal user);
        bool CanAccessActivity(Activity activity, ClaimsPrincipal user);

        IQueryable<Customer> FilterCustomers(IQueryable<Customer> query, ClaimsPrincipal user);
        IQueryable<Lead> FilterLeads(IQueryable<Lead> query, ClaimsPrincipal user);
        IQueryable<Opportunity> FilterOpportunities(IQueryable<Opportunity> query, ClaimsPrincipal user);
        IQueryable<FollowUp> FilterFollowUps(IQueryable<FollowUp> query, ClaimsPrincipal user);
        IQueryable<Activity> FilterActivities(IQueryable<Activity> query, ClaimsPrincipal user);
    }
}
