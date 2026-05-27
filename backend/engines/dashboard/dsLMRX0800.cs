using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using BimasaktiReports.FinancialReports.Backend.Services;

namespace BimasaktiReports.FinancialReports.Backend.Engines.Dashboard
{
    [ApiController]
    [Route("api")]
    public class dsLMRX0800 : ControllerBase
    {
        private readonly IsvcDashboardAnalyticsService _dashboardAnalyticsService;

        public dsLMRX0800(IsvcDashboardAnalyticsService dashboardAnalyticsService)
        {
            _dashboardAnalyticsService = dashboardAnalyticsService;
        }

        // --- Property Maintenance Desk Status ---
        [HttpGet("v1/dashboard/maintenance/status")]
        public async Task<IActionResult> GetMaintenanceStatus(
            [FromQuery(Name = "company_id")] string companyId = "ASHMD",
            [FromQuery] string? year = null,
            [FromQuery] string? period = null)
        {
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            var analyticsResult = await _dashboardAnalyticsService.GetMaintenanceStatusAsync(databasePath, companyId, year, period);
            return Ok(analyticsResult);
        }
    }
}
