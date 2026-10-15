using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace AcxiomCRM.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICrmAccessService _crmAccess;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportService(
            ApplicationDbContext context,
            ICrmAccessService crmAccess,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _crmAccess = crmAccess;
            _userManager = userManager;
        }

        public async Task<List<Customer>> GetCustomerReportAsync(ClaimsPrincipal user, CustomerStatus? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Customers.Include(c => c.Owner).AsNoTracking();
            query = _crmAccess.FilterCustomers(query, user);

            if (status.HasValue) query = query.Where(c => c.Status == status.Value);
            if (fromDate.HasValue) query = query.Where(c => c.CreatedDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(c => c.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(c => c.CreatedDate).ToListAsync();
        }

        public async Task<List<Lead>> GetLeadReportAsync(ClaimsPrincipal user, LeadStatus? status, LeadSource? source, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Leads.Include(l => l.AssignedUser).Include(l => l.Customer).AsNoTracking();
            query = _crmAccess.FilterLeads(query, user);

            if (status.HasValue) query = query.Where(l => l.Status == status.Value);
            if (source.HasValue) query = query.Where(l => l.Source == source.Value);
            if (fromDate.HasValue) query = query.Where(l => l.CreatedDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(l => l.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(l => l.CreatedDate).ToListAsync();
        }

        public async Task<List<FollowUp>> GetFollowUpReportAsync(ClaimsPrincipal user, FollowUpStatus? status, FollowUpType? type, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.FollowUps.Include(f => f.AssignedUser).Include(f => f.Customer).Include(f => f.Lead).AsNoTracking();
            query = _crmAccess.FilterFollowUps(query, user);

            if (status.HasValue) query = query.Where(f => f.Status == status.Value);
            if (type.HasValue) query = query.Where(f => f.FollowUpType == type.Value);
            if (fromDate.HasValue) query = query.Where(f => f.FollowUpDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(f => f.FollowUpDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        }

        public async Task<List<Opportunity>> GetOpportunityReportAsync(ClaimsPrincipal user, OpportunityStage? stage, OpportunityStatus? status, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Opportunities.Include(o => o.Customer).Include(o => o.AssignedUser).AsNoTracking();
            query = _crmAccess.FilterOpportunities(query, user);

            if (stage.HasValue) query = query.Where(o => o.Stage == stage.Value);
            if (status.HasValue) query = query.Where(o => o.Status == status.Value);
            if (fromDate.HasValue) query = query.Where(o => o.CreatedDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(o => o.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(o => o.Amount).ToListAsync();
        }

        public async Task<List<PipelineReportItem>> GetPipelineReportAsync(ClaimsPrincipal user)
        {
            var query = _context.Opportunities.AsNoTracking();
            query = _crmAccess.FilterOpportunities(query, user);

            var items = await query
                .GroupBy(o => o.Stage)
                .Select(g => new PipelineReportItem
                {
                    Stage = g.Key.ToString(),
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    WeightedAmount = g.Sum(x => x.Amount * x.Probability / 100m)
                })
                .ToListAsync();

            return items;
        }

        public async Task<ConversionReportDto> GetConversionReportAsync(ClaimsPrincipal user, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.Leads.AsNoTracking();
            query = _crmAccess.FilterLeads(query, user);

            if (fromDate.HasValue) query = query.Where(l => l.CreatedDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(l => l.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            var total = await query.CountAsync();
            var converted = await query.CountAsync(l => l.Status == LeadStatus.Converted);
            var lost = await query.CountAsync(l => l.Status == LeadStatus.Lost);
            var active = total - converted - lost;

            var conversionRate = total > 0 ? Math.Round(((decimal)converted / total) * 100m, 2) : 0m;

            // Sum of opportunities created from converted leads
            var convertedLeadIds = await query.Where(l => l.Status == LeadStatus.Converted).Select(l => l.LeadId).ToListAsync();
            var oppSum = await _context.Opportunities
                .Where(o => o.LeadId.HasValue && convertedLeadIds.Contains(o.LeadId.Value))
                .SumAsync(o => (decimal?)o.Amount) ?? 0m;

            return new ConversionReportDto
            {
                TotalLeads = total,
                ConvertedLeads = converted,
                LostLeads = lost,
                ActiveLeads = active,
                ConversionRatePercent = conversionRate,
                TotalConvertedPipeline = oppSum
            };
        }

        public async Task<List<UserActivityReportItem>> GetUserActivityReportAsync(ClaimsPrincipal user)
        {
            if (!_crmAccess.IsAdmin(user) && !_crmAccess.IsManager(user))
            {
                return new List<UserActivityReportItem>();
            }

            var users = await _userManager.Users.AsNoTracking().ToListAsync();
            var report = new List<UserActivityReportItem>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var customersCount = await _context.Customers.CountAsync(c => c.OwnerId == u.Id);
                var leadsCount = await _context.Leads.CountAsync(l => l.AssignedTo == u.Id);
                var oppsCount = await _context.Opportunities.CountAsync(o => o.AssignedTo == u.Id);
                var followUpsCount = await _context.FollowUps.CountAsync(f => f.AssignedTo == u.Id);
                var activitiesCount = await _context.Activities.CountAsync(a => a.AssignedTo == u.Id);
                var auditCount = await _context.AuditLogs.CountAsync(a => a.UserId == u.Id);

                report.Add(new UserActivityReportItem
                {
                    UserId = u.Id,
                    UserName = u.FullName ?? u.UserName ?? "User",
                    Role = roles.FirstOrDefault() ?? "None",
                    CustomersCount = customersCount,
                    LeadsCount = leadsCount,
                    OpportunitiesCount = oppsCount,
                    FollowUpsCount = followUpsCount,
                    ActivitiesCount = activitiesCount,
                    AuditActionsCount = auditCount
                });
            }

            return report;
        }

        public async Task<List<AuditLog>> GetAuditReportAsync(ClaimsPrincipal user, string? module, string? action, DateTime? fromDate, DateTime? toDate)
        {
            if (!_crmAccess.IsAdmin(user))
            {
                return new List<AuditLog>();
            }

            var query = _context.AuditLogs.Include(a => a.User).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(module)) query = query.Where(a => a.EntityName == module);
            if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
            if (fromDate.HasValue) query = query.Where(a => a.CreatedDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(a => a.CreatedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            return await query.OrderByDescending(a => a.CreatedDate).Take(200).ToListAsync();
        }

        public byte[] GenerateCsv<T>(IEnumerable<T> data, Func<T, string[]> rowMapper, string[] headers)
        {
            var sb = new StringBuilder();

            // Headers
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));

            // Rows
            foreach (var item in data)
            {
                var fields = rowMapper(item);
                sb.AppendLine(string.Join(",", fields.Select(EscapeCsvValue)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        private static string EscapeCsvValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";

            var clean = value.Trim();

            // Prevent CSV Formula Injection
            if (clean.StartsWith("=") || clean.StartsWith("+") || clean.StartsWith("-") || clean.StartsWith("@"))
            {
                clean = "'" + clean;
            }

            clean = clean.Replace("\"", "\"\"");
            return $"\"{clean}\"";
        }
    }
}
