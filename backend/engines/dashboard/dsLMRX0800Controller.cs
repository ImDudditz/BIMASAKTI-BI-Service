using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using BiPortal.FinancialReports.Backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace BiPortal.FinancialReports.Backend.Engines.Dashboard
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class dsLMRX0800Controller : ControllerBase
    {
        private readonly IsvcDashboardAnalyticsService _dashboardAnalyticsService;

        public dsLMRX0800Controller(IsvcDashboardAnalyticsService dashboardAnalyticsService)
        {
            _dashboardAnalyticsService = dashboardAnalyticsService;
        }

        // --- Property Maintenance Desk Status ---
        [HttpGet("v1/dashboard/maintenance/status")]
        public async Task<IActionResult> GetMaintenanceStatus(
            [FromQuery] string? year = null,
            [FromQuery] string? period = null)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            var analyticsResult = await _dashboardAnalyticsService.GetMaintenanceStatusAsync(databasePath, companyId, year, period);
            return Ok(analyticsResult);
        }
    }
}
