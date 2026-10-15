using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Customers(CustomerStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetCustomerReportAsync(User, status, fromDate, toDate);
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View(data);
        }

        public async Task<IActionResult> ExportCustomersCsv(CustomerStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetCustomerReportAsync(User, status, fromDate, toDate);
            var headers = new[] { "Customer Code", "Customer Name", "Email", "Phone", "Company", "City", "State", "Status", "Owner", "Created Date" };

            var csv = _reportService.GenerateCsv(data, c => new[]
            {
                c.CustomerCode,
                c.CustomerName,
                c.Email,
                c.Phone,
                c.CompanyName ?? "",
                c.City ?? "",
                c.State ?? "",
                c.Status.ToString(),
                c.Owner?.FullName ?? c.Owner?.UserName ?? "",
                c.CreatedDate.ToString("yyyy-MM-dd HH:mm")
            }, headers);

            return File(csv, "text/csv", $"Customer_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        public async Task<IActionResult> Leads(LeadStatus? status = null, LeadSource? source = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetLeadReportAsync(User, status, source, fromDate, toDate);
            ViewBag.Status = status;
            ViewBag.Source = source;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View(data);
        }

        public async Task<IActionResult> ExportLeadsCsv(LeadStatus? status = null, LeadSource? source = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetLeadReportAsync(User, status, source, fromDate, toDate);
            var headers = new[] { "Lead Code", "Lead Name", "Email", "Phone", "Company", "Source", "Status", "Priority", "Expected Value", "Assigned To", "Created Date" };

            var csv = _reportService.GenerateCsv(data, l => new[]
            {
                l.LeadCode,
                l.LeadName,
                l.Email ?? "",
                l.Phone ?? "",
                l.CompanyName ?? "",
                l.Source.ToString(),
                l.Status.ToString(),
                l.Priority.ToString(),
                l.ExpectedValue.ToString("F2"),
                l.AssignedUser?.FullName ?? l.AssignedUser?.UserName ?? "",
                l.CreatedDate.ToString("yyyy-MM-dd HH:mm")
            }, headers);

            return File(csv, "text/csv", $"Lead_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        public async Task<IActionResult> FollowUps(FollowUpStatus? status = null, FollowUpType? type = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetFollowUpReportAsync(User, status, type, fromDate, toDate);
            ViewBag.Status = status;
            ViewBag.Type = type;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View(data);
        }

        public async Task<IActionResult> ExportFollowUpsCsv(FollowUpStatus? status = null, FollowUpType? type = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetFollowUpReportAsync(User, status, type, fromDate, toDate);
            var headers = new[] { "Subject", "Date", "Type", "Status", "Related Customer", "Related Lead", "Assigned To" };

            var csv = _reportService.GenerateCsv(data, f => new[]
            {
                f.Subject,
                f.FollowUpDate.ToString("yyyy-MM-dd HH:mm"),
                f.FollowUpType.ToString(),
                f.Status.ToString(),
                f.Customer?.CustomerName ?? "",
                f.Lead?.LeadName ?? "",
                f.AssignedUser?.FullName ?? f.AssignedUser?.UserName ?? ""
            }, headers);

            return File(csv, "text/csv", $"FollowUp_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        public async Task<IActionResult> Opportunities(OpportunityStage? stage = null, OpportunityStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetOpportunityReportAsync(User, stage, status, fromDate, toDate);
            ViewBag.Stage = stage;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View(data);
        }

        public async Task<IActionResult> ExportOpportunitiesCsv(OpportunityStage? stage = null, OpportunityStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetOpportunityReportAsync(User, stage, status, fromDate, toDate);
            var headers = new[] { "Opportunity Name", "Customer", "Amount", "Stage", "Probability %", "Weighted Value", "Expected Close Date", "Status", "Assigned To" };

            var csv = _reportService.GenerateCsv(data, o => new[]
            {
                o.OpportunityName,
                o.Customer?.CustomerName ?? "",
                o.Amount.ToString("F2"),
                o.Stage.ToString(),
                o.Probability.ToString(),
                o.WeightedPipelineValue.ToString("F2"),
                o.ExpectedCloseDate.ToString("yyyy-MM-dd"),
                o.Status.ToString(),
                o.AssignedUser?.FullName ?? o.AssignedUser?.UserName ?? ""
            }, headers);

            return File(csv, "text/csv", $"Opportunity_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
        }

        public async Task<IActionResult> Pipeline()
        {
            var data = await _reportService.GetPipelineReportAsync(User);
            return View(data);
        }

        public async Task<IActionResult> Conversions(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetConversionReportAsync(User, fromDate, toDate);
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View(data);
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
        public async Task<IActionResult> UserActivity()
        {
            var data = await _reportService.GetUserActivityReportAsync(User);
            return View(data);
        }

        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Audit(string? module = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var data = await _reportService.GetAuditReportAsync(User, module, action, fromDate, toDate);
            ViewBag.Module = module;
            ViewBag.Action = action;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            return View(data);
        }
    }
}
