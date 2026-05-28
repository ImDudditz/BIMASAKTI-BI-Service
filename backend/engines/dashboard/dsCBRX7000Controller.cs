using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Threading.Tasks;
using BimasaktiReports.FinancialReports.Backend.Services;

namespace BimasaktiReports.FinancialReports.Backend.Engines.Dashboard
{
    [ApiController]
    [Route("api")]
    public class dsCBRX7000 : ControllerBase
    {
        private readonly IsvcCBRX7000 _cashFlowService;

        public dsCBRX7000(IsvcCBRX7000 cashFlowService)
        {
            _cashFlowService = cashFlowService;
        }

        [HttpGet("v1/dashboard/cashbook/cashflow")]
        public async Task<IActionResult> GetOperatingCashflow(
            [FromQuery(Name = "company_id")] string companyId = "ASHMD",
            [FromQuery] string? year = null)
        {
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

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
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }
    }
}
