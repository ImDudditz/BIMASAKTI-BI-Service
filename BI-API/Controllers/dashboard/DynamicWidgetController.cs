using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Services;
using Bimasakti.BiService.Api.Models;
using Bimasakti.BiService.Api.Core;
using System.Linq;
using System.IO;
using Microsoft.AspNetCore.Authorization;

namespace Bimasakti.BiService.Api.Controllers.Dashboard
{
    [Authorize]
    [ApiController]
    [Route("api/dynamic-widgets")]
    public class DynamicWidgetController : ControllerBase
    {
        private readonly IWidgetConfigService _widgetConfigService;
        private readonly IDynamicDataService _dynamicDataService;

        public DynamicWidgetController(IWidgetConfigService widgetConfigService, IDynamicDataService dynamicDataService)
        {
            _widgetConfigService = widgetConfigService;
            _dynamicDataService = dynamicDataService;
        }

        [HttpGet("available")]
        public IActionResult GetAvailableWidgets([FromQuery] string username)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            var allWidgets = _widgetConfigService.GetAvailableWidgets();

            string databasePath = DbUtils.GetSafeDbPath(companyId);
            using var db = new CompanyDbContext(databasePath);
            var user = db.Users.FirstOrDefault(u => u.Username.ToUpper() == username.ToUpper());
            if (user == null) return Unauthorized();

            if (user.Role.Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(allWidgets);
            }

            var allowedWidgetKeys = db.UserWidgets
                .Where(w => w.UserId == user.Id && w.IsActive)
                .Select(w => w.WidgetKey)
                .ToList();

            var userWidgets = allWidgets.Where(w => allowedWidgetKeys.Contains(w.Id)).ToList();
            return Ok(userWidgets);
        }

        public class WidgetDataRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("year")]
            public string? Year { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("period")]
            public string? Period { get; set; }
        }

        [HttpPost("data/{category}/{id}")]
        public async Task<IActionResult> GetWidgetData(string category, string id, [FromBody] WidgetDataRequest? req)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            var config = _widgetConfigService.GetWidgetConfig(category, id);
            if (config == null) return NotFound();

            string databasePath = DbUtils.GetSafeDbPath(companyId);
            var data = await _dynamicDataService.ExecuteWidgetQueryAsync(databasePath, config.Query, req?.Year, req?.Period);

            return Ok(data);
        }

        [HttpPost("save")]
        public IActionResult SaveWidget([FromBody] DsbiWidgetConfig config)
        {
            var success = _widgetConfigService.SaveWidgetConfig(config);
            if (success) return Ok(new { success = true });
            return BadRequest("Failed to save widget.");
        }

        [HttpDelete("{category}/{id}")]
        public IActionResult DeleteWidget(string category, string id)
        {
            var success = _widgetConfigService.DeleteWidgetConfig(category, id);
            if (success) return Ok(new { success = true });
            return BadRequest("Failed to delete widget.");
        }
    }
}
