using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class ConvertLeadModel
    {
        public int LeadId { get; set; }
        public bool CreateOpportunity { get; set; } = true;
        public string? OpportunityName { get; set; }
        public decimal? OpportunityAmount { get; set; }
        public DateTime? ExpectedCloseDate { get; set; }
    }

    public interface ILeadService
    {
        Task<(bool Success, string? Error, Lead? Lead)> CreateLeadAsync(Lead lead, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error, Lead? Lead)> UpdateLeadAsync(Lead lead, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> ChangeStatusAsync(int leadId, LeadStatus newStatus, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error, Customer? Customer, Opportunity? Opportunity)> ConvertLeadAsync(ConvertLeadModel model, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> DeleteLeadAsync(int leadId, ClaimsPrincipal user, string? ipAddress = null);
        Task<Lead?> GetLeadByIdAsync(int leadId, ClaimsPrincipal user);
        Task<(List<Lead> Items, int TotalCount)> GetLeadsAsync(ClaimsPrincipal user, string? search, LeadStatus? status, LeadSource? source, string? assignedTo, string? sortBy, bool descending, int page, int pageSize);
        bool IsValidStatusTransition(LeadStatus currentStatus, LeadStatus targetStatus, bool isAdminOrManager);
    }
}
