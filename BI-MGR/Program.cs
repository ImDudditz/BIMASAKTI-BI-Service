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
using Bimasakti.BiService.Api.Engines;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Bimasakti.BiService.Mgr.Models;
using Bimasakti.BiService.Mgr.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ManagerCorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Allow all for manager portal locally
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (args.Contains("--sync-all") || (args.Length > 0 && args[0] == "--sync"))
{
    var syncService = new DatabaseSyncService();
    if (args.Contains("--sync-all"))
    {
        using var db = new CentralDbContext(CentralDbUtils.GetCentralDbPath());
        var companies = await db.Companies.Where(c => c.IsActive).ToListAsync();
        foreach (var c in companies)
        {
            Console.WriteLine($"\n[Sync] Company: {c.CompanyId}");
            var res = await syncService.SyncCompanyDatabaseAsync(c.CompanyId);
            Console.WriteLine($"[Sync] {(res.Success ? "SUCCESS" : $"FAILED: {res.Message}")}");
        }
    }
    else
    {
        string companyId = args.Length > 1 ? args[1] : "ASHMD";
        var res = await syncService.SyncCompanyDatabaseAsync(companyId);
        Console.WriteLine($"[Sync] {(res.Success ? "SUCCESS" : $"FAILED")}: {res.Message}");
    }
    Environment.Exit(0);
}

// --- ISO 27001 & INDO LAW SAFE MIDDLEWARE ---
app.Use(async (context, next) =>
{
    var wibZone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");
    var wibTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wibZone);

    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://unpkg.com; img-src 'self' data:;");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

    string path = context.Request.Path.Value ?? "";
    string ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";
    string qs = context.Request.QueryString.Value ?? "";

    // Indo Law PII Masking & XSS Sanitization Logging
    if (qs.Contains("@") || qs.Contains("ktp") || qs.Contains("phone") || qs.Contains("<script>"))
    {
        qs = "?[MASKED_PII_OR_SANITIZED]";
    }

    Console.WriteLine($"[ACCESS - {wibTime:yyyy-MM-dd HH:mm:ss} WIB] {ip} {context.Request.Method} {path}{qs}");
    await next();
});

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

// Secure SuperAdmin Password Initialization
string adminPass = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD") ?? "";
string secretFilePath = app.Configuration.GetValue<string>("Config:SuperAdminSecretPath", "admin_secret.txt")!;
secretFilePath = Path.Combine(Directory.GetCurrentDirectory(), secretFilePath);

if (string.IsNullOrEmpty(adminPass) && File.Exists(secretFilePath))
{
    try { adminPass = File.ReadAllText(secretFilePath).Trim(); } catch { }
}

if (string.IsNullOrEmpty(adminPass))
{
    var bytes = new byte[16];
    System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
    adminPass = Convert.ToBase64String(bytes).Replace("/", "").Replace("+", "").Replace("=", "").Substring(0, 16);

    try { File.WriteAllText(secretFilePath, adminPass); } catch { }

    Console.WriteLine("=====================================================");
    Console.WriteLine("WARNING: SUPERADMIN_PASSWORD env variable was not set!");
    Console.WriteLine($"A secure random password has been generated: {adminPass}");
    Console.WriteLine($"Password has been saved to: {secretFilePath}");
    Console.WriteLine("=====================================================");
}
Environment.SetEnvironmentVariable("SUPERADMIN_PASSWORD", adminPass);

// Auth
app.MapPost("/api/login", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var doc = JsonDocument.Parse(await reader.ReadToEndAsync());
    string pass = doc.RootElement.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

    string currentAdminPass = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD") ?? "";

    if (!string.IsNullOrEmpty(pass) && pass.Trim() == currentAdminPass.Trim())
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "superadmin") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.Ok(new { message = "Logged in" });
    }
    Console.WriteLine($"[Auth] Failed login attempt.");
    return Results.Unauthorized();
});

app.MapPost("/api/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { message = "Logged out successfully" });
});

app.MapGet("/api/me", (HttpContext ctx) => ctx.User.Identity?.IsAuthenticated == true ? Results.Ok(new { status = "Authenticated" }) : Results.Unauthorized());

app.MapGet("/api/internal/companies/{id}/status", async (string id) =>
{
    string companyId = id.Trim().ToUpperInvariant();
    using var db = new CentralDbContext(CentralDbUtils.GetCentralDbPath());
    var comp = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);
    return Results.Ok(new { isActive = comp?.IsActive ?? false });
});

app.MapPost("/api/internal/companies/{id}/sync", (string id) =>
{
    string companyId = id.Trim().ToUpperInvariant();
    Task.Run(async () =>
    {
        try
        {
            await new DatabaseSyncService().SyncCompanyDatabaseAsync(companyId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Sync error: " + ex.Message);
        }
    });
    return Results.Ok(new { message = $"Sync started in background for {id}" });
});

// Admin group
var adminGroup = app.MapGroup("/api").RequireAuthorization();

adminGroup.MapGet("/status", () => Results.Ok(new { message = "SaaS Manager Running" }));

adminGroup.MapGet("/companies", async () =>
{
    using var db = new CentralDbContext(CentralDbUtils.GetCentralDbPath());
    return Results.Ok(await db.Companies.Select(c => c.CompanyId).ToListAsync());
});

adminGroup.MapGet("/companies/details", async () =>
{
    using var db = new CentralDbContext(CentralDbUtils.GetCentralDbPath());
    return Results.Ok(await db.Companies.ToListAsync());
});

adminGroup.MapGet("/companies/{id}", async (string id) =>
{
    string companyId = id.Trim().ToUpperInvariant();
    using var db = new CentralDbContext(CentralDbUtils.GetCentralDbPath());
    var comp = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);

    string[] urls = Array.Empty<string>();
    if (comp != null)
    {
        try
        {
            var doc = JsonDocument.Parse(comp.SyncConfigJson);
            if (doc.RootElement.TryGetProperty("sync_urls", out var urlsProp))
                urls = urlsProp.EnumerateObject().Select(x => x.Value.GetString()!).ToArray();
        }
        catch { }
    }
    return Results.Ok(new { companyId, urls, isActive = comp?.IsActive ?? false });
});

adminGroup.MapPost("/companies", async (PortalCompanySaveRequest req) =>
{
    string companyId = req.CompanyId.Trim().ToUpperInvariant();

    // Parse URLs
    var dict = new Dictionary<string, string>();
    foreach (var line in (req.SyncUrls ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
    {
        string t = line.Trim();
        if (string.IsNullOrEmpty(t)) continue;
        string key = string.Concat(Path.GetFileNameWithoutExtension(t).Where(c => char.IsLetterOrDigit(c) || c == '_'));
        if (!string.IsNullOrEmpty(key) && char.IsLetter(key[0])) dict[key] = t;
    }

    string syncConfigJson = JsonSerializer.Serialize(new { sync_urls = dict }, new JsonSerializerOptions { WriteIndented = true });

    using var db = new CentralDbContext(CentralDbUtils.GetCentralDbPath());
    var comp = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == companyId);

    if (req.Mode == "New" && comp == null)
    {
        if (dict.Count == 0) return Results.BadRequest(new { message = "At least one valid sync URL required." });
        comp = new Company { CompanyId = companyId, SyncConfigJson = syncConfigJson, IsActive = req.IsActive };
        db.Companies.Add(comp);
    }
    else if (comp != null)
    {
        comp.SyncConfigJson = syncConfigJson;
        comp.IsActive = req.IsActive;
    }
    else
    {
        return Results.BadRequest(new { message = "Company not found for edit." });
    }
    await db.SaveChangesAsync();

    using var tenantDb = new TenantDbContext(svcDbUtils.GetSafeDbPath(companyId));
    tenantDb.Database.EnsureCreated();

    var authSvc = new Bimasakti.BiService.Api.Services.svcAuthenticationService();
    var allWidgets = new[] { "kpi_cards", "capital_growth", "operating_cash_flow", "revenue_budget", "expense_budget", "operation_metrics", "lease_expirations", "tickets_kpi", "maintenance_status", "tickets_by_category" };
    var allReports = new[] { "balance_sheet", "income_statement" };

    Action<string, string> ensureUser = (username, pass) =>
    {
        var u = tenantDb.Users.FirstOrDefault(x => x.Username == username);
        if (u == null)
        {
            u = new User { Username = username, PasswordHash = authSvc.GetPasswordHash(pass), Role = "admin", CompanyId = companyId, IsActive = true };
            tenantDb.Users.Add(u);
            tenantDb.SaveChanges();
            foreach (var w in allWidgets) tenantDb.UserWidgets.Add(new UserWidget { UserId = u.Id, WidgetKey = w, IsActive = true });
            foreach (var r in allReports) tenantDb.UserReports.Add(new UserReport { UserId = u.Id, ReportKey = r, IsActive = true });
        }
    };

    ensureUser("sa", "sfbmub");
    ensureUser("realta", "nvctgc");
    tenantDb.SaveChanges();

    return Results.Ok(new { message = $"Company {companyId} processed successfully!" });
});

adminGroup.MapPost("/companies/{id}/sync", (string id) =>
{
    string companyId = id.Trim().ToUpperInvariant();
    Task.Run(async () =>
    {
        try
        {
            await new DatabaseSyncService().SyncCompanyDatabaseAsync(companyId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Sync error: " + ex.Message);
        }
    });
    return Results.Ok(new { message = $"Sync started in background for {id}" });
});

adminGroup.MapGet("/manager/companies/{companyId}/users", async (string companyId) =>
{
    try
    {
        using var db = new TenantDbContext(svcDbUtils.GetSafeDbPath(companyId.Trim().ToUpperInvariant()));
        await db.Database.EnsureCreatedAsync();
        var users = await db.Users.Where(u => u.CompanyId == companyId).ToListAsync();
        return Results.Ok(users.Select(u => new { id = u.Id, username = u.Username, role = u.Role }));
    }
    catch { return Results.StatusCode(500); }
});

adminGroup.MapGet("/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId) =>
{
    try
    {
        using var db = new TenantDbContext(svcDbUtils.GetSafeDbPath(companyId.Trim().ToUpperInvariant()));
        await db.Database.EnsureCreatedAsync();
        return Results.Ok(new
        {
            widgets = await db.UserWidgets.Where(w => w.UserId == userId).ToDictionaryAsync(w => w.WidgetKey, w => w.IsActive),
            reports = await db.UserReports.Where(r => r.UserId == userId).ToDictionaryAsync(r => r.ReportKey, r => r.IsActive)
        });
    }
    catch { return Results.StatusCode(500); }
});

adminGroup.MapPost("/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId, PermissionSettingsSpecification permissions) =>
{
    try
    {
        using var db = new TenantDbContext(svcDbUtils.GetSafeDbPath(companyId.Trim().ToUpperInvariant()));
        await db.Database.EnsureCreatedAsync();

        var exW = await db.UserWidgets.Where(w => w.UserId == userId).ToDictionaryAsync(w => w.WidgetKey);
        foreach (var kvp in permissions.Widgets)
        {
            if (exW.TryGetValue(kvp.Key, out var w)) w.IsActive = kvp.Value;
            else db.UserWidgets.Add(new UserWidget { UserId = userId, WidgetKey = kvp.Key, IsActive = kvp.Value });
        }

        var exR = await db.UserReports.Where(r => r.UserId == userId).ToDictionaryAsync(r => r.ReportKey);
        foreach (var kvp in permissions.Reports)
        {
            if (exR.TryGetValue(kvp.Key, out var r)) r.IsActive = kvp.Value;
            else db.UserReports.Add(new UserReport { UserId = userId, ReportKey = kvp.Key, IsActive = kvp.Value });
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { message = "Permissions updated successfully." });
    }
    catch { return Results.StatusCode(500); }
});

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID")))
{
    app.Run("http://localhost:8003");
}
else
{
    app.Run();
}

public class PortalCompanySaveRequest
{
    public string CompanyId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string SyncUrls { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
