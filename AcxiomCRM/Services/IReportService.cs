using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class PipelineReportItem
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class ConversionReportDto
    {
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public int LostLeads { get; set; }
        public int ActiveLeads { get; set; }
        public decimal ConversionRatePercent { get; set; }
        public decimal TotalConvertedPipeline { get; set; }
    }

    public class UserActivityReportItem
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int CustomersCount { get; set; }
        public int LeadsCount { get; set; }
        public int OpportunitiesCount { get; set; }
        public int FollowUpsCount { get; set; }
        public int ActivitiesCount { get; set; }
        public int AuditActionsCount { get; set; }
    }

    public interface IReportService
    {
        Task<List<Customer>> GetCustomerReportAsync(ClaimsPrincipal user, CustomerStatus? status, DateTime? fromDate, DateTime? toDate);
        Task<List<Lead>> GetLeadReportAsync(ClaimsPrincipal user, LeadStatus? status, LeadSource? source, DateTime? fromDate, DateTime? toDate);
        Task<List<FollowUp>> GetFollowUpReportAsync(ClaimsPrincipal user, FollowUpStatus? status, FollowUpType? type, DateTime? fromDate, DateTime? toDate);
        Task<List<Opportunity>> GetOpportunityReportAsync(ClaimsPrincipal user, OpportunityStage? stage, OpportunityStatus? status, DateTime? fromDate, DateTime? toDate);
        Task<List<PipelineReportItem>> GetPipelineReportAsync(ClaimsPrincipal user);
        Task<ConversionReportDto> GetConversionReportAsync(ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate);
        Task<List<UserActivityReportItem>> GetUserActivityReportAsync(ClaimsPrincipal user);
        Task<List<AuditLog>> GetAuditReportAsync(ClaimsPrincipal user, string? module, string? action, DateTime? fromDate, DateTime? toDate);

        byte[] GenerateCsv<T>(IEnumerable<T> data, Func<T, string[]> rowMapper, string[] headers);
    }
}
