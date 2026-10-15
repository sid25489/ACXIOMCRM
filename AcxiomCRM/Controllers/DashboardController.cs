using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        public async Task<IActionResult> Index(
            string dateFilter = "all",
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var model = await _dashboardService.GetDashboardDataAsync(User, dateFilter, fromDate, toDate);
            return View(model);
        }
    }
}
