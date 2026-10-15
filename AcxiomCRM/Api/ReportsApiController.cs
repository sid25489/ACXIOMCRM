using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsApiController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly IDashboardService _dashboardService;

        public ReportsApiController(IReportService reportService, IDashboardService dashboardService)
        {
            _reportService = reportService;
            _dashboardService = dashboardService;
        }

        [HttpGet("pipeline")]
        public async Task<IActionResult> GetPipelineReport()
        {
            var data = await _reportService.GetPipelineReportAsync(User);
            return Ok(new
            {
                generatedAt = DateTime.UtcNow,
                totalStages = data.Count,
                totalPipelineValue = data.Sum(x => x.TotalAmount),
                totalWeightedValue = data.Sum(x => x.WeightedAmount),
                stages = data
            });
        }

        [HttpGet("conversions")]
        public async Task<IActionResult> GetConversionReport([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var data = await _reportService.GetConversionReportAsync(User, fromDate, toDate);
            return Ok(data);
        }

        [HttpGet("sales")]
        public async Task<IActionResult> GetSalesReport([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
        {
            var wonOpps = await _reportService.GetOpportunityReportAsync(User, OpportunityStage.Won, OpportunityStatus.Won, fromDate, toDate);

            return Ok(new
            {
                generatedAt = DateTime.UtcNow,
                totalWonCount = wonOpps.Count,
                totalWonAmount = wonOpps.Sum(o => o.Amount),
                deals = wonOpps.Select(o => new
                {
                    opportunityId = o.OpportunityId,
                    name = o.OpportunityName,
                    customerName = o.Customer?.CustomerName,
                    amount = o.Amount,
                    closedDate = o.ExpectedCloseDate,
                    ownerName = o.AssignedUser?.FullName ?? o.AssignedUser?.UserName
                })
            });
        }
    }
}
