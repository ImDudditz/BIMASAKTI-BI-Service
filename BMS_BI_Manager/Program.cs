using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using BMS_BI_SERVICE.Core.Engines;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => {
    options.AddPolicy("ManagerCorsPolicy", policy => {
        policy.SetIsOriginAllowed(origin => {
            if (string.IsNullOrEmpty(origin)) return false;
            try {
                var uri = new Uri(origin);
                return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
            } catch {
                return false;
            }
        })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "manager_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors("ManagerCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
});

string url = "http://localhost:8003";

// Secure SuperAdmin Password Initialization
string? adminPass = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD");
if (string.IsNullOrEmpty(adminPass))
{
    var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
    var bytes = new byte[16];
    rng.GetBytes(bytes);
    adminPass = Convert.ToBase64String(bytes).Replace("/", "").Replace("+", "").Replace("=", "").Substring(0, 16);
    Environment.SetEnvironmentVariable("SUPERADMIN_PASSWORD", adminPass);
    Console.WriteLine("=====================================================");
    Console.WriteLine("WARNING: SUPERADMIN_PASSWORD env variable was not set!");
    Console.WriteLine($"A secure random password has been generated: {adminPass}");
    Console.WriteLine("=====================================================");
}

// Auth
app.MapPost("/api/login", async (HttpContext ctx) => {
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    var doc = JsonDocument.Parse(body);
    string pass = doc.RootElement.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";
    
    adminPass = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD")!;
    
    if (pass == adminPass) {
        var claims = new List<System.Security.Claims.Claim> {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "superadmin")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return Results.Ok(new { message = "Logged in" });
    }
    return Results.Unauthorized();
});

app.MapPost("/api/logout", async (HttpContext ctx) => {
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Logged out successfully" });
});

app.MapGet("/api/me", (HttpContext ctx) => {
    if (ctx.User.Identity?.IsAuthenticated == true) return Results.Ok(new { status = "Authenticated" });
    return Results.Unauthorized();
});

// Admin group
var adminGroup = app.MapGroup("/api").RequireAuthorization();

adminGroup.MapGet("/status", () => {
    return Results.Ok(new { message = "SaaS Manager Running" });
});

// Companies
adminGroup.MapGet("/companies", async () => {
    string centralDbPath = svcDbUtils.GetCentralDbPath();
    using var db = new CentralDbContext(centralDbPath);
    var companies = await db.Companies.ToListAsync();
    return Results.Ok(companies.Select(c => c.CompanyId).ToList());
});

adminGroup.MapGet("/companies/details", async () => {
    string centralDbPath = svcDbUtils.GetCentralDbPath();
    using var db = new CentralDbContext(centralDbPath);
    var companies = await db.Companies.ToListAsync();
    return Results.Ok(companies);
});

adminGroup.MapGet("/companies/{id}", async (string id) => {
    string companyId = id.Trim().ToUpperInvariant();
    string centralDbPath = svcDbUtils.GetCentralDbPath();
    using var db = new CentralDbContext(centralDbPath);
    var comp = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
    
    string[] urls = Array.Empty<string>();
    if (comp != null) {
        try {
            var doc = JsonDocument.Parse(comp.SyncConfigJson);
            if (doc.RootElement.TryGetProperty("sync_urls", out var urlsProp))
                urls = urlsProp.EnumerateObject().Select(x => x.Value.GetString()!).ToArray();
        } catch {}
    }
    return Results.Ok(new { companyId, urls, isActive = comp?.IsActive ?? false });
});

adminGroup.MapPost("/companies", async (PortalCompanySaveRequest req) => {
    string companyId = req.CompanyId.Trim().ToUpperInvariant();
    string centralDbPath = svcDbUtils.GetCentralDbPath();
    
    // Parse URLs
    var dict = new Dictionary<string, string>();
    foreach (var line in (req.SyncUrls ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
        string t = line.Trim();
        if (string.IsNullOrEmpty(t)) continue;
        string key = string.Concat(Path.GetFileNameWithoutExtension(t).Where(c => char.IsLetterOrDigit(c) || c == '_'));
        if (!string.IsNullOrEmpty(key) && char.IsLetter(key[0])) dict[key] = t;
    }
    
    string syncConfigJson = JsonSerializer.Serialize(new { sync_urls = dict }, new JsonSerializerOptions { WriteIndented = true });

    using var db = new CentralDbContext(centralDbPath);
    var comp = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
    
    if (req.Mode == "New" && comp == null) {
        if (dict.Count == 0) return Results.BadRequest(new { message = "At least one valid sync URL required." });
        comp = new Company { CompanyId = companyId, SyncConfigJson = syncConfigJson, IsActive = req.IsActive };
        db.Companies.Add(comp);
        
        // Ensure directory and TenantDB exist natively
        svcDbUtils.GetSafeDbPath(companyId);
    } else if (comp != null) {
        comp.SyncConfigJson = syncConfigJson;
        comp.IsActive = req.IsActive;
    } else {
        return Results.BadRequest(new { message = "Company not found for edit." });
    }
    await db.SaveChangesAsync();

    return Results.Ok(new { message = $"Company {companyId} processed successfully!" });
});

adminGroup.MapPost("/companies/{id}/sync", (string id) => {
    string companyId = id.Trim().ToUpperInvariant();
    Task.Run(async () => {
        try {
            var syncSvc = new BMS_BI_SERVICE.Core.Services.svcDatabaseSyncService();
            await syncSvc.SyncCompanyDatabaseAsync(companyId);
        } catch (Exception ex) {
            Console.WriteLine("Sync error: " + ex.Message);
        }
    });
    return Results.Ok(new { message = $"Sync started in background for {id}" });
});

// User Permissions
adminGroup.MapGet("/manager/companies/{companyId}/users", async (string companyId) => {
    string id = companyId.Trim().ToUpperInvariant();
    string dbPath = svcDbUtils.GetSafeDbPath(id);
    try {
        using var db = new TenantDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();
        var users = await db.Users.Where(u => u.CompanyId == id && u.Role != "admin").ToListAsync();
        return Results.Ok(users.Select(u => new { id = u.Id, username = u.Username, role = u.Role }));
    } catch { return Results.StatusCode(500); }
});

adminGroup.MapGet("/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId) => {
    string id = companyId.Trim().ToUpperInvariant();
    string dbPath = svcDbUtils.GetSafeDbPath(id);
    try {
        using var db = new TenantDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();
        var widgets = await db.UserWidgets.Where(w => w.UserId == userId).ToListAsync();
        var reports = await db.UserReports.Where(r => r.UserId == userId).ToListAsync();
        return Results.Ok(new {
            widgets = widgets.ToDictionary(w => w.WidgetKey, w => w.IsActive),
            reports = reports.ToDictionary(r => r.ReportKey, r => r.IsActive)
        });
    } catch { return Results.StatusCode(500); }
});

adminGroup.MapPost("/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId, BMS_BI_SERVICE.Core.Engines.PermissionSettingsSpecification permissions) => {
    string id = companyId.Trim().ToUpperInvariant();
    string dbPath = svcDbUtils.GetSafeDbPath(id);
    try {
        using var db = new TenantDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();
        
        var exW = await db.UserWidgets.Where(w => w.UserId == userId).ToDictionaryAsync(w => w.WidgetKey);
        foreach (var kvp in permissions.Widgets) {
            if (exW.TryGetValue(kvp.Key, out var w)) w.IsActive = kvp.Value;
            else db.UserWidgets.Add(new UserWidget { UserId = userId, WidgetKey = kvp.Key, IsActive = kvp.Value });
        }

        var exR = await db.UserReports.Where(r => r.UserId == userId).ToDictionaryAsync(r => r.ReportKey);
        foreach (var kvp in permissions.Reports) {
            if (exR.TryGetValue(kvp.Key, out var r)) r.IsActive = kvp.Value;
            else db.UserReports.Add(new UserReport { UserId = userId, ReportKey = kvp.Key, IsActive = kvp.Value });
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Permissions updated successfully." });
    } catch { return Results.StatusCode(500); }
});

app.Run(url);

public class PortalCompanySaveRequest {
    public string CompanyId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string SyncUrls { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
