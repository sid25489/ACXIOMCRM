using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public interface IOpportunityService
    {
        Task<(bool Success, string? Error, Opportunity? Opportunity)> CreateOpportunityAsync(Opportunity opportunity, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error, Opportunity? Opportunity)> UpdateOpportunityAsync(Opportunity opportunity, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> ChangeStageAsync(int opportunityId, OpportunityStage newStage, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> DeleteOpportunityAsync(int opportunityId, ClaimsPrincipal user, string? ipAddress = null);
        Task<Opportunity?> GetOpportunityByIdAsync(int opportunityId, ClaimsPrincipal user);
        Task<(List<Opportunity> Items, int TotalCount)> GetOpportunitiesAsync(ClaimsPrincipal user, string? search, OpportunityStage? stage, OpportunityStatus? status, string? assignedTo, int? customerId, string? sortBy, bool descending, int page, int pageSize);
        Task<(decimal TotalPipeline, decimal WeightedPipeline, int OpenDeals, int WonDeals, int LostDeals)> GetPipelineSummaryAsync(ClaimsPrincipal user);
    }
}
