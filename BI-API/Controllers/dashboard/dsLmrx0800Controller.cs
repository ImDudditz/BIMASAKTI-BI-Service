using Bimasakti.BiService.Api.Controllers;
using Bimasakti.BiService.Api.Core;
using Bimasakti.BiService.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Services;
using Bimasakti.BiService.Api.Services.Engines;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;

namespace Bimasakti.BiService.Api.Controllers.Dashboard
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class dsLmrx0800Controller : ControllerBase
    {
        private readonly IDashboardAnalyticsService _dashboardAnalyticsService;
        private readonly IsvcLmrx0800 _service;

        public dsLmrx0800Controller(IDashboardAnalyticsService dashboardAnalyticsService, IsvcLmrx0800 service)
        {
            _dashboardAnalyticsService = dashboardAnalyticsService;
            _service = service;
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

            string databasePath = DbUtils.GetSafeDbPath(companyId);

            var analyticsResult = await _dashboardAnalyticsService.GetMaintenanceStatusAsync(databasePath, companyId, year, period);
            return Ok(analyticsResult);
        }

        [HttpGet("engine/lm/tickets/summary/{companyId}")]
        public async Task<IActionResult> GetTicketsSummary([FromRoute] string companyId)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !companyId.Equals(companyIdClaim, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { detail = "Access to requested company database is denied." });
            }

            try
            {
                var response = await _service.GetTicketsSummaryAsync(companyId);
                if (response == null)
                {
                    return NotFound(new { detail = $"Database for company '{companyId}' not found." });
                }
                return Ok(response);
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { detail = "The tickets table (LMRX0800) does not exist for this company." });
            }
            catch (SqliteException)
            {
                return StatusCode(500, new { detail = "A database operational error occurred while processing the request." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { detail = "An unexpected error occurred while compiling the semantic ticket dataset." });
            }
        }

        [HttpGet("engine/lmrx0800/kpi/{companyId}")]
        public async Task<IActionResult> GetTicketKpis(
            [FromRoute] string companyId,
            [FromQuery] string year,
            [FromQuery] string period)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !companyId.Equals(companyIdClaim, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { detail = "Access to requested company database is denied." });
            }

            try
            {
                var response = await _service.GetTicketKpisAsync(companyId, year, period);
                if (response == null)
                {
                    return NotFound(new { detail = $"Database for company '{companyId}' not found." });
                }
                return Ok(response);
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { detail = "The tickets table (LMRX0800) does not exist for this company." });
            }
            catch (SqliteException)
            {
                return StatusCode(500, new { detail = "A database operational error occurred." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { detail = "An unexpected error occurred while fetching ticket KPIs." });
            }
        }
    }
}


