using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace BimasaktiReports.FinancialReports.Backend.Engines
{
    // --- SPECIFICATION CLASSES ---

    public class WidgetLayoutSpecification
    {
        [System.Text.Json.Serialization.JsonPropertyName("widget_key")]
        public string WidgetKey { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;

        [System.Text.Json.Serialization.JsonPropertyName("layout_order")]
        public int LayoutOrder { get; set; } = 0;

        [System.Text.Json.Serialization.JsonPropertyName("config")]
        public string? Config { get; set; } // Represented as serialized JSON string in SQLite
    }

    public class PermissionSettingsSpecification
    {
        public Dictionary<string, bool> Widgets { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, bool> Reports { get; set; } = new Dictionary<string, bool>();
    }

    // --- MAIN API CLASS ---

    [Authorize]
    [ApiController]
    [Route("api")]
    public class dsAdminDashboardController : ControllerBase
    {
        // Helper to enforce admin check
        private async Task<User?> RequireAdmin(TenantDbContext dbContext, string username, string companyId)
        {
            var adminUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Username == username && user.CompanyId == companyId);
            if (adminUser == null || adminUser.Role != "admin")
            {
                return null;
            }
            return adminUser;
        }

        // --- 1. User Dashboard Widgets ---

        [HttpGet("dashboard/my-widgets")]
        public async Task<IActionResult> GetUserWidgets()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            var usernameClaim = HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(companyIdClaim) || string.IsNullOrEmpty(usernameClaim))
            {
                return Unauthorized(new { detail = "Invalid token claims" });
            }

            string companyId = companyIdClaim.ToUpperInvariant();
            string username = usernameClaim;
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var dbContext = new TenantDbContext(databasePath))
                {
                    await dbContext.Database.EnsureCreatedAsync();

                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.CompanyId == companyId);
                    if (user == null)
                    {
                        return NotFound(new { detail = "User not found" });
                    }

                    var userWidgets = await dbContext.UserWidgets
                         .Where(widget => widget.UserId == user.Id && widget.IsActive)
                         .OrderBy(widget => widget.LayoutOrder)
                         .ToListAsync();

                    if (userWidgets.Count == 0)
                    {
                        // Default widgets if none configured
                        var defaultWidgetKeys = new[] { "kpi_cards", "capital_growth", "operating_cash_flow", "revenue_budget", "expense_budget" };
                        return Ok(defaultWidgetKeys.Select(key => new WidgetLayoutSpecification { WidgetKey = key }));
                    }

                    return Ok(userWidgets.Select(widget => new WidgetLayoutSpecification
                    {
                        WidgetKey = widget.WidgetKey,
                        IsActive = widget.IsActive,
                        LayoutOrder = widget.LayoutOrder,
                        Config = widget.Config
                    }));
                }
            }
            catch (Exception)
            {
                return StatusCode(500, new { detail = "An unexpected system error occurred." });
            }
        }

        [HttpGet("dashboard/my-reports")]
        public async Task<IActionResult> GetUserReports()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            var usernameClaim = HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(companyIdClaim) || string.IsNullOrEmpty(usernameClaim))
            {
                return Unauthorized(new { detail = "Invalid token claims" });
            }

            string companyId = companyIdClaim.ToUpperInvariant();
            string username = usernameClaim;
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var dbContext = new TenantDbContext(databasePath))
                {
                    await dbContext.Database.EnsureCreatedAsync();

                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.CompanyId == companyId);
                    if (user == null)
                    {
                        return NotFound(new { detail = "User not found" });
                    }

                    var userReports = await dbContext.UserReports
                         .Where(report => report.UserId == user.Id && report.IsActive)
                         .ToListAsync();

                    if (userReports.Count == 0 && user.Role != "admin")
                    {
                        // Default reports if none configured
                        var defaultReportKeys = new[] { "balance_sheet", "income_statement" };
                        return Ok(defaultReportKeys);
                    }

                    if (user.Role == "admin")
                    {
                        // Admins always have access to all reports
                        return Ok(new[] { "balance_sheet", "income_statement" });
                    }

                    return Ok(userReports.Select(report => report.ReportKey));
                }
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { detail = exception.Message });
            }
        }

        [HttpPost("dashboard/my-widgets")]
        public async Task<IActionResult> UpdateUserWidgets(
            [FromBody] List<WidgetLayoutSpecification> widgets)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            var usernameClaim = HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(companyIdClaim) || string.IsNullOrEmpty(usernameClaim))
            {
                return Unauthorized(new { detail = "Invalid token claims" });
            }

            string companyId = companyIdClaim.ToUpperInvariant();
            string username = usernameClaim;
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var dbContext = new TenantDbContext(databasePath))
                {
                    await dbContext.Database.EnsureCreatedAsync();

                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.CompanyId == companyId);
                    if (user == null)
                    {
                        return NotFound(new { detail = "User not found" });
                    }

                    var existingWidgetsList = await dbContext.UserWidgets.Where(widget => widget.UserId == user.Id).ToListAsync();
                    var existingWidgetMap = existingWidgetsList.ToDictionary(widget => widget.WidgetKey);

                    foreach (var layoutSpecification in widgets)
                    {
                        if (existingWidgetMap.TryGetValue(layoutSpecification.WidgetKey, out var widgetToUpdate))
                        {
                            widgetToUpdate.IsActive = layoutSpecification.IsActive;
                            widgetToUpdate.LayoutOrder = layoutSpecification.LayoutOrder;
                            widgetToUpdate.Config = layoutSpecification.Config;
                        }
                        else
                        {
                            dbContext.UserWidgets.Add(new UserWidget
                            {
                                UserId = user.Id,
                                WidgetKey = layoutSpecification.WidgetKey,
                                IsActive = layoutSpecification.IsActive,
                                LayoutOrder = layoutSpecification.LayoutOrder,
                                Config = layoutSpecification.Config
                            });
                        }
                    }

                    await dbContext.SaveChangesAsync();
                }

                return Ok(new { message = "User widgets updated successfully" });
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { detail = exception.Message });
            }
        }

        // --- 2. Admin Users list ---

        [HttpGet("admin/users")]
        public async Task<IActionResult> GetAdminUsers()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            var adminUsername = HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(companyIdClaim) || string.IsNullOrEmpty(adminUsername))
            {
                return Unauthorized(new { detail = "Invalid token claims" });
            }

            string companyId = companyIdClaim.ToUpperInvariant();
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var dbContext = new TenantDbContext(databasePath))
                {
                    await dbContext.Database.EnsureCreatedAsync();

                    var adminUser = await RequireAdmin(dbContext, adminUsername, companyId);
                    if (adminUser == null)
                    {
                        return StatusCode(403, new { detail = "Admin access required" });
                    }

                    var usersList = await dbContext.Users.Where(user => user.CompanyId == companyId).ToListAsync();
                    return Ok(usersList.Select(user => new { id = user.Id, username = user.Username, role = user.Role }));
                }
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { detail = exception.Message });
            }
        }

        // --- 3. Permissions Management ---

        [HttpGet("admin/users/{userIdParameter}/permissions")]
        public async Task<IActionResult> GetUserPermissions(
            [FromRoute] int userIdParameter)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            var adminUsername = HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(companyIdClaim) || string.IsNullOrEmpty(adminUsername))
            {
                return Unauthorized(new { detail = "Invalid token claims" });
            }

            string companyId = companyIdClaim.ToUpperInvariant();
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var dbContext = new TenantDbContext(databasePath))
                {
                    await dbContext.Database.EnsureCreatedAsync();

                    var adminUser = await RequireAdmin(dbContext, adminUsername, companyId);
                    if (adminUser == null)
                    {
                        return StatusCode(403, new { detail = "Admin access required" });
                    }

                    var targetUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == userIdParameter && user.CompanyId == companyId);
                    if (targetUser == null)
                    {
                        return NotFound(new { detail = "User not found" });
                    }

                    var widgetsList = await dbContext.UserWidgets.Where(widget => widget.UserId == userIdParameter).ToListAsync();
                    var reportsList = await dbContext.UserReports.Where(report => report.UserId == userIdParameter).ToListAsync();

                    return Ok(new
                    {
                        widgets = widgetsList.ToDictionary(widget => widget.WidgetKey, widget => widget.IsActive),
                        reports = reportsList.ToDictionary(report => report.ReportKey, report => report.IsActive)
                    });
                }
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { detail = exception.Message });
            }
        }

        [HttpPost("admin/users/{userIdParameter}/permissions")]
        public async Task<IActionResult> UpdateUserPermissions(
            [FromRoute] int userIdParameter,
            [FromBody] PermissionSettingsSpecification permissionSettings)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            var adminUsername = HttpContext.User.Identity?.Name;

            if (string.IsNullOrEmpty(companyIdClaim) || string.IsNullOrEmpty(adminUsername))
            {
                return Unauthorized(new { detail = "Invalid token claims" });
            }

            string companyId = companyIdClaim.ToUpperInvariant();
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var dbContext = new TenantDbContext(databasePath))
                {
                    await dbContext.Database.EnsureCreatedAsync();

                    var adminUser = await RequireAdmin(dbContext, adminUsername, companyId);
                    if (adminUser == null)
                    {
                        return StatusCode(403, new { detail = "Admin access required" });
                    }

                    var targetUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == userIdParameter && user.CompanyId == companyId);
                    if (targetUser == null)
                    {
                        return NotFound(new { detail = "User not found" });
                    }

                    var existingWidgetsList = await dbContext.UserWidgets.Where(widget => widget.UserId == userIdParameter).ToListAsync();
                    var existingWidgetsMap = existingWidgetsList.ToDictionary(widget => widget.WidgetKey);

                    foreach (var keyValuePair in permissionSettings.Widgets)
                    {
                        if (existingWidgetsMap.TryGetValue(keyValuePair.Key, out var widget))
                        {
                            widget.IsActive = keyValuePair.Value;
                        }
                        else
                        {
                            dbContext.UserWidgets.Add(new UserWidget { UserId = userIdParameter, WidgetKey = keyValuePair.Key, IsActive = keyValuePair.Value });
                        }
                    }

                    var existingReportsList = await dbContext.UserReports.Where(report => report.UserId == userIdParameter).ToListAsync();
                    var existingReportsMap = existingReportsList.ToDictionary(report => report.ReportKey);

                    foreach (var keyValuePair in permissionSettings.Reports)
                    {
                        if (existingReportsMap.TryGetValue(keyValuePair.Key, out var report))
                        {
                            report.IsActive = keyValuePair.Value;
                        }
                        else
                        {
                            dbContext.UserReports.Add(new UserReport { UserId = userIdParameter, ReportKey = keyValuePair.Key, IsActive = keyValuePair.Value });
                        }
                    }

                    await dbContext.SaveChangesAsync();
                }

                return Ok(new { status = "success", message = "Permissions updated" });
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { detail = exception.Message });
            }
        }

        // --- 4. Export excel report endpoint ---

        [HttpPost("export/excel")]
        public IActionResult ExportExcelReport([FromBody] ExcelReportPayload payload)
        {
            try
            {
                var excelStream = prFinancialExcel.CreateExcelReport(payload);
                string companyName = (payload.Company ?? "Financial_Report").Replace(" ", "_");
                string filename = $"{companyName}_{payload.PeriodName}_{payload.Year}.xlsx";

                return File(excelStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { detail = exception.Message });
            }
        }

        // --- 5. Export PDF report endpoint ---

        [HttpPost("export/pdf")]
        public IActionResult ExportPdfReport([FromBody] ExcelReportPayload payload)
        {
            try
            {
                var pdfStream = prFinancialPDF.CreatePdfReport(payload);
                string companyName = (payload.Company ?? "Financial_Report").Replace(" ", "_");
                string filename = $"{companyName}_{payload.PeriodName}_{payload.Year}.pdf";

                return File(pdfStream, "application/pdf", filename);
            }
            catch (Exception)
            {
                return StatusCode(500, new { detail = "An unexpected system error occurred." });
            }
        }
    }
}
