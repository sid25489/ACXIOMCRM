using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;

        public DashboardService(ApplicationDbContext context, ICrmAccessService crmAccess)
        {
            _context = context;
            _crmAccess = crmAccess;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(
            ClaimsPrincipal user,
            string dateFilter = "all",
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var now = DateTime.UtcNow;
            DateTime? filterStart = null;
            DateTime? filterEnd = null;

            switch (dateFilter.ToLower())
            {
                case "today":
                    filterStart = now.Date;
                    filterEnd = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "thisweek":
                    // Start from Monday
                    var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    filterStart = now.Date.AddDays(-1 * diff);
                    filterEnd = filterStart.Value.AddDays(7).AddTicks(-1);
                    break;
                case "thismonth":
                    filterStart = new DateTime(now.Year, now.Month, 1);
                    filterEnd = filterStart.Value.AddMonths(1).AddTicks(-1);
                    break;
                case "custom":
                    if (fromDate.HasValue) filterStart = fromDate.Value.Date;
                    if (toDate.HasValue) filterEnd = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    break;
            }

            // Customer Queries
            var customerQuery = _context.Customers.AsNoTracking();
            customerQuery = _crmAccess.FilterCustomers(customerQuery, user);
            if (filterStart.HasValue) customerQuery = customerQuery.Where(c => c.CreatedDate >= filterStart.Value);
            if (filterEnd.HasValue) customerQuery = customerQuery.Where(c => c.CreatedDate <= filterEnd.Value);
            var totalCustomers = await customerQuery.CountAsync();

            // Lead Queries
            var leadQuery = _context.Leads.AsNoTracking();
            leadQuery = _crmAccess.FilterLeads(leadQuery, user);
            if (filterStart.HasValue) leadQuery = leadQuery.Where(l => l.CreatedDate >= filterStart.Value);
            if (filterEnd.HasValue) leadQuery = leadQuery.Where(l => l.CreatedDate <= filterEnd.Value);

            var totalLeads = await leadQuery.CountAsync();
            var openLeads = await leadQuery.CountAsync(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost);

            // Opportunity Queries
            var oppQuery = _context.Opportunities.AsNoTracking();
            oppQuery = _crmAccess.FilterOpportunities(oppQuery, user);
            if (filterStart.HasValue) oppQuery = oppQuery.Where(o => o.CreatedDate >= filterStart.Value);
            if (filterEnd.HasValue) oppQuery = oppQuery.Where(o => o.CreatedDate <= filterEnd.Value);

            var totalOpps = await oppQuery.CountAsync();
            var openOpps = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Open);
            var wonOpps = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Won);
            var lostOpps = await oppQuery.CountAsync(o => o.Status == OpportunityStatus.Lost);

            var openOppsQuery = oppQuery.Where(o => o.Status == OpportunityStatus.Open);
            var totalOpenPipeline = await openOppsQuery.SumAsync(o => (decimal?)o.Amount) ?? 0m;
            var weightedPipeline = await openOppsQuery.SumAsync(o => (decimal?)(o.Amount * o.Probability / 100m)) ?? 0m;

            // Follow-Up Queries
            var followUpQuery = _context.FollowUps.AsNoTracking();
            followUpQuery = _crmAccess.FilterFollowUps(followUpQuery, user);

            var today = now.Date;
            var upcomingEnd = today.AddDays(7).AddDays(1).AddTicks(-1);

            var upcomingFollowUps = await followUpQuery.CountAsync(f =>
                f.Status == FollowUpStatus.Planned &&
                f.FollowUpDate >= today &&
                f.FollowUpDate <= upcomingEnd);

            var overdueFollowUps = await followUpQuery.CountAsync(f =>
                f.Status == FollowUpStatus.Planned &&
                f.FollowUpDate.Date < today);

            // Chart 1: Lead Status Distribution
            var leadStatusCounts = await leadQuery
                .GroupBy(l => l.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var chart1 = new ChartDatasetDto
            {
                Labels = new List<string> { "New", "Contacted", "Qualified", "Converted", "Lost" },
                Values = new List<decimal>
                {
                    leadStatusCounts.FirstOrDefault(x => x.Status == LeadStatus.New)?.Count ?? 0,
                    leadStatusCounts.FirstOrDefault(x => x.Status == LeadStatus.Contacted)?.Count ?? 0,
                    leadStatusCounts.FirstOrDefault(x => x.Status == LeadStatus.Qualified)?.Count ?? 0,
                    leadStatusCounts.FirstOrDefault(x => x.Status == LeadStatus.Converted)?.Count ?? 0,
                    leadStatusCounts.FirstOrDefault(x => x.Status == LeadStatus.Lost)?.Count ?? 0
                },
                BackgroundColors = new List<string> { "#3b82f6", "#06b6d4", "#10b981", "#8b5cf6", "#ef4444" }
            };

            // Chart 2: Opportunity Pipeline by Stage
            var oppStageGroup = await oppQuery
                .GroupBy(o => o.Stage)
                .Select(g => new { Stage = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToListAsync();

            var chart2 = new ChartDatasetDto
            {
                Labels = new List<string> { "Qualification", "Proposal", "Negotiation", "Won", "Lost" },
                Values = new List<decimal>
                {
                    oppStageGroup.FirstOrDefault(x => x.Stage == OpportunityStage.Qualification)?.Amount ?? 0m,
                    oppStageGroup.FirstOrDefault(x => x.Stage == OpportunityStage.Proposal)?.Amount ?? 0m,
                    oppStageGroup.FirstOrDefault(x => x.Stage == OpportunityStage.Negotiation)?.Amount ?? 0m,
                    oppStageGroup.FirstOrDefault(x => x.Stage == OpportunityStage.Won)?.Amount ?? 0m,
                    oppStageGroup.FirstOrDefault(x => x.Stage == OpportunityStage.Lost)?.Amount ?? 0m
                },
                BackgroundColors = new List<string> { "#6366f1", "#0ea5e9", "#f59e0b", "#10b981", "#ef4444" }
            };

            // Chart 3: Monthly Sales (Past 6 Months Won Deals)
            var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
            var wonOppsInPeriod = await _context.Opportunities
                .AsNoTracking()
                .Where(o => o.Status == OpportunityStatus.Won && o.ExpectedCloseDate >= sixMonthsAgo)
                .ToListAsync();

            // Filter in-memory for authorized records
            wonOppsInPeriod = wonOppsInPeriod
                .Where(o => _crmAccess.CanAccessOpportunity(o, user))
                .ToList();

            var chart3Labels = new List<string>();
            var chart3Values = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var monthLabel = monthDate.ToString("MMM yyyy");
                var sum = wonOppsInPeriod
                    .Where(o => o.ExpectedCloseDate.Year == monthDate.Year && o.ExpectedCloseDate.Month == monthDate.Month)
                    .Sum(o => o.Amount);

                chart3Labels.Add(monthLabel);
                chart3Values.Add(sum);
            }

            var chart3 = new ChartDatasetDto
            {
                Labels = chart3Labels,
                Values = chart3Values,
                BackgroundColors = new List<string> { "#10b981" }
            };

            return new DashboardViewModel
            {
                DateFilter = dateFilter,
                FromDate = fromDate,
                ToDate = toDate,
                Kpis = new DashboardKpiDto
                {
                    TotalCustomers = totalCustomers,
                    TotalLeads = totalLeads,
                    OpenLeads = openLeads,
                    TotalOpportunities = totalOpps,
                    OpenOpportunities = openOpps,
                    WonOpportunities = wonOpps,
                    LostOpportunities = lostOpps,
                    TotalOpenPipelineValue = totalOpenPipeline,
                    WeightedOpenPipelineValue = weightedPipeline,
                    UpcomingFollowUps = upcomingFollowUps,
                    OverdueFollowUps = overdueFollowUps
                },
                Charts = new DashboardChartsDto
                {
                    LeadStatusDistribution = chart1,
                    PipelineByStage = chart2,
                    MonthlySales = chart3
                }
            };
        }
    }
}
