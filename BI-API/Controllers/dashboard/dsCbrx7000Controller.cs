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

namespace Bimasakti.BiService.Api.Controllers.Dashboard
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class dsCbrx7000Controller : ControllerBase
    {
        private readonly IsvcCbrx7000 _cashFlowService;

        public dsCbrx7000Controller(IsvcCbrx7000 cashFlowService)
        {
            _cashFlowService = cashFlowService;
        }

        [HttpGet("v1/dashboard/cashbook/cashflow")]
        public async Task<IActionResult> GetOperatingCashflow(
            [FromQuery] string? year = null)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = DbUtils.GetSafeDbPath(companyId);

            if (!System.IO.File.Exists(databasePath))
            {
                return NotFound(new { status = "error", message = "Database not found" });
            }

            if (string.IsNullOrEmpty(year))
            {
                year = DateTime.Now.Year.ToString();
            }

            try
            {
                var result = await _cashFlowService.GetOperatingCashFlowAsync(databasePath, year);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = "error", message = "An unexpected system error occurred." });
            }
        }
    }
}


