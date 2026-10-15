using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class DashboardKpiDto
    {
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalOpenPipelineValue { get; set; }
        public decimal WeightedOpenPipelineValue { get; set; }
        public int UpcomingFollowUps { get; set; }
        public int OverdueFollowUps { get; set; }
    }

    public class ChartDatasetDto
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Values { get; set; } = new();
        public List<string> BackgroundColors { get; set; } = new();
    }

    public class DashboardChartsDto
    {
        public ChartDatasetDto LeadStatusDistribution { get; set; } = new();
        public ChartDatasetDto PipelineByStage { get; set; } = new();
        public ChartDatasetDto MonthlySales { get; set; } = new();
    }

    public class DashboardViewModel
    {
        public DashboardKpiDto Kpis { get; set; } = new();
        public DashboardChartsDto Charts { get; set; } = new();
        public string DateFilter { get; set; } = "all";
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardDataAsync(ClaimsPrincipal user, string dateFilter = "all", DateTime? fromDate = null, DateTime? toDate = null);
    }
}
