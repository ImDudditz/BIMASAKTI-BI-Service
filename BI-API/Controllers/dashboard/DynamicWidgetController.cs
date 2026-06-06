using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Services;
using Bimasakti.BiService.Api.Models;
using System.Linq;
using System.IO;

namespace Bimasakti.BiService.Api.Controllers.dashboard
{
    [ApiController]
    [Route("api/dynamic-widgets")]
    public class DynamicWidgetController : ControllerBase
    {
        private readonly IWidgetConfigService _widgetConfigService;
        private readonly IDynamicDataService _dynamicDataService;
        private readonly string _centralDbPath;

        public DynamicWidgetController(IWidgetConfigService widgetConfigService, IDynamicDataService dynamicDataService)
        {
            _widgetConfigService = widgetConfigService;
            _dynamicDataService = dynamicDataService;
            _centralDbPath = Path.Combine(Directory.GetCurrentDirectory(), "BMS_BI.db");
        }

        [HttpGet("available")]
        public IActionResult GetAvailableWidgets([FromQuery] string username)
        {
            var allWidgets = _widgetConfigService.GetAvailableWidgets();

            using var db = new CompanyDbContext(_centralDbPath);
            var user = db.Users.FirstOrDefault(u => u.Username == username);
            if (user == null) return Unauthorized();

            if (user.Role == "Admin")
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

        [HttpPost("data/{category}/{id}")]
        public async Task<IActionResult> GetWidgetData(string category, string id, [FromQuery] string companyId)
        {
            var config = _widgetConfigService.GetWidgetConfig(category, id);
            if (config == null) return NotFound();

            // Typically tenant DBs are under a 'Tenants' or similar folder.
            // Depending on architecture, they might just be in the root directory if it's dynamic.
            // I'll assume they're in a 'Tenants' directory for this implementation, but can adjust if needed.
            var tenantDbPath = Path.Combine(Directory.GetCurrentDirectory(), "Tenants", $"{companyId}.db");
            if (!System.IO.File.Exists(tenantDbPath))
            {
                tenantDbPath = Path.Combine(Directory.GetCurrentDirectory(), $"{companyId}.db"); // fallback
            }

            var data = await _dynamicDataService.ExecuteWidgetQueryAsync(tenantDbPath, config.Query);

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
