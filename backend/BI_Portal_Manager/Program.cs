using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using BiPortal.FinancialReports.Backend.Engines;

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

var app = builder.Build();

app.UseCors("ManagerCorsPolicy");
app.UseDefaultFiles();
app.UseStaticFiles();

// Dynamic port configuration from appsettings.json
int managerPort = PortalManagerCore.GetManagerPortFromSettings();
string url = $"http://localhost:{managerPort}";

PortalManagerCore.CheckExternalServers();

// Status
app.MapGet("/api/status", async () => {
    int currentBack = PortalManagerCore.BackendProcessActive ? PortalManagerCore.BackendRunningPort : PortalManagerCore.RefreshBackendPort();
    int currentFront = PortalManagerCore.FrontendProcessActive ? PortalManagerCore.FrontendRunningPort : PortalManagerCore.RefreshFrontendPort();

    bool backendActive = await PortalManagerCore.IsPortActiveAsync("127.0.0.1", currentBack);
    bool frontendActive = await PortalManagerCore.IsPortActiveAsync("127.0.0.1", currentFront);

    string backendStatus = PortalManagerCore.BackendHasError ? "Error" : (backendActive ? "Running" : "Stopped");
    string frontendStatus = PortalManagerCore.FrontendHasError ? "Error" : (frontendActive ? "Running" : "Stopped");

    return Results.Ok(new {
        backendActive,
        frontendActive,
        backendStatus,
        frontendStatus,
        godMode = File.Exists(PortalManagerCore.GodModePath),
        localIp = PortalManagerCore.LocalIp,
        coolAlias = PortalManagerCore.CoolAlias,
        backendPort = currentBack,
        frontendPort = currentFront,
        managerPort
    });
});

app.MapPost("/api/start", () => { PortalManagerCore.StartAll(); return Results.Ok(new { message = "Started." }); });
app.MapPost("/api/stop", () => { PortalManagerCore.StopAll(); return Results.Ok(new { message = "Stopped." }); });
app.MapPost("/api/restart", () => { PortalManagerCore.RestartAll(); return Results.Ok(new { message = "Restarted." }); });
app.MapPost("/api/godmode", () => { PortalManagerCore.ToggleGodMode(); return Results.Ok(new { godMode = File.Exists(PortalManagerCore.GodModePath) }); });
app.MapGet("/api/logs", () => Results.Ok(PortalManagerCore.LogQueue.ToArray()));
app.MapPost("/api/logs/clear", () => { PortalManagerCore.LogQueue.Clear(); return Results.Ok(); });

// Companies
app.MapGet("/api/companies", () => {
    var list = new List<string>();
    try {
        string assetsDir = Path.GetFullPath(Path.Combine(PortalManagerCore.GetRootDirectory(), "backend", "assets"));
        if (Directory.Exists(assetsDir)) {
            list.AddRange(Directory.GetDirectories(assetsDir)
                .Select(Path.GetFileName)
                .Where(n => n.Length == 5 && PortalManagerCore.IsValidId(n)));
        }
    } catch (Exception ex) { PortalManagerCore.Log("Error: " + ex.Message); }
    return Results.Ok(list);
});

// Users
app.MapGet("/api/manager/companies/{companyId}/users", async (string companyId) => {
    string id = companyId.Trim().ToUpperInvariant();
    string dbPath = svcDbUtils.GetSafeDbPath(id);
    try {
        using var db = new TenantDbContext(dbPath);
        await db.Database.EnsureCreatedAsync();
        var users = await db.Users.Where(u => u.CompanyId == id && u.Role != "admin").ToListAsync();
        return Results.Ok(users.Select(u => new { id = u.Id, username = u.Username, role = u.Role }));
    } catch { return Results.StatusCode(500); }
});

// Permissions GET
app.MapGet("/api/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId) => {
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

// Permissions POST
app.MapPost("/api/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId, PermissionSettingsSpecification permissions) => {
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

app.MapGet("/api/companies/{id}", (string id) => {
    string companyId = id.Trim().ToUpperInvariant();
    string[] urls = PortalManagerCore.LoadCompanySyncUrls(companyId);
    return Results.Ok(new { companyId, urls });
});

app.MapPost("/api/companies", (PortalCompanySaveRequest req) => {
    string companyId = req.CompanyId.Trim().ToUpperInvariant();
    string rootDir = PortalManagerCore.GetRootDirectory();
    string assetsDir = Path.GetFullPath(Path.Combine(rootDir, "backend", "assets"));
    string tmplDir = Path.GetFullPath(Path.Combine(assetsDir, "BMS"));
    string targetDir = Path.GetFullPath(Path.Combine(assetsDir, companyId));

    if (req.Mode == "New") {
        var validUrlsCount = 0;
        foreach (var url in (req.SyncUrls ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            string t = url.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            string key = string.Concat(Path.GetFileNameWithoutExtension(t).Where(c => char.IsLetterOrDigit(c) || c == '_'));
            if (!string.IsNullOrEmpty(key) && char.IsLetter(key[0])) {
                validUrlsCount++;
            }
        }
        if (validUrlsCount == 0) {
            return Results.BadRequest(new { message = "At least one synchronization URL starting with a letter-based endpoint name is required." });
        }

        Directory.CreateDirectory(targetDir);
        foreach (string dirPath in Directory.GetDirectories(tmplDir, "*", SearchOption.AllDirectories)) {
            Directory.CreateDirectory(dirPath.Replace(tmplDir, targetDir));
        }
        string tmplSyncPath = Path.Combine(tmplDir, "BMS_sync.py");
        if (File.Exists(tmplSyncPath)) {
            string targetSyncPath = Path.Combine(targetDir, companyId + "_sync.py");
            File.Copy(tmplSyncPath, targetSyncPath, true);
            string content = File.ReadAllText(targetSyncPath);
            content = content.Replace("BMS", companyId).Replace("bms", companyId.ToLowerInvariant());
            File.WriteAllText(targetSyncPath, content);
        }
    }
    
    PortalManagerCore.WriteCompanyConfig(targetDir, companyId, req.SyncUrls ?? "");
    PortalManagerCore.TriggerDatabaseSync(companyId);
    return Results.Ok(new { message = $"Company {companyId} processed successfully!" });
});

app.MapPost("/api/companies/{id}/sync", (string id) => {
    PortalManagerCore.TriggerDatabaseSync(id.Trim().ToUpperInvariant());
    return Results.Ok(new { message = $"Sync started for {id}" });
});


app.Run(url);


// --- CORE SYSTEM MANAGER ---
public static class PortalManagerCore {
    private static Process _backendProcess;
    private static Process _frontendProcess;
    public static bool BackendHasError { get; private set; }
    public static bool FrontendHasError { get; private set; }
    
    public static ConcurrentQueue<string> LogQueue { get; } = new();
    
    public static string LocalIp { get; }
    public static string CoolAlias { get; }
    public static string GodModePath { get; }

    public static int FrontendRunningPort { get; private set; }
    public static int BackendRunningPort { get; private set; }

    public static bool BackendProcessActive => _backendProcess != null && !_backendProcess.HasExited;
    public static bool FrontendProcessActive => _frontendProcess != null && !_frontendProcess.HasExited;

    static PortalManagerCore() {
        LocalIp = GetLocalIp();
        CoolAlias = Dns.GetHostName().ToLowerInvariant() + ".local";
        GodModePath = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend", "engines", ".god_mode_enabled"));
        BackendRunningPort = RefreshBackendPort();
        FrontendRunningPort = RefreshFrontendPort();
    }

    public static void Log(string message, bool toConsole = true) {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (toConsole) Console.WriteLine(line);
        LogQueue.Enqueue(line);
        while (LogQueue.Count > 1000) LogQueue.TryDequeue(out _);
    }

    public static int GetManagerPortFromSettings() {
        try {
            string path = Path.Combine(GetRootDirectory(), "backend", "appsettings.json");
            if (File.Exists(path)) {
                var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("Manager", out var mgr) && mgr.TryGetProperty("Port", out var port))
                    return port.GetInt32();
            }
        } catch {}
        return 8003; // default if not found
    }

    public static int RefreshBackendPort() {
        try {
            string path = Path.Combine(GetRootDirectory(), "backend", "appsettings.json");
            if (File.Exists(path)) {
                var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("Server", out var svr) && svr.TryGetProperty("Port", out var port))
                    return port.GetInt32();
            }
        } catch {}
        return 8001;
    }

    public static int RefreshFrontendPort() {
        try {
            string path = Path.Combine(GetRootDirectory(), "frontend", "vite.config.js");
            if (File.Exists(path)) {
                var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path), @"port:\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int p)) return p;
            }
        } catch {}
        return 8002;
    }

    public static void StartBackend() {
        try {
            BackendHasError = false;
            string dir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend"));
            BackendRunningPort = RefreshBackendPort();
            KillProcessOnPort(BackendRunningPort);

            string dll = Path.Combine(dir, "bin", "Debug", "net8.0", "BiPortal.FinancialReports.Backend.dll");
            string args = File.Exists(dll) ? $"\"{dll}\"" : "run";

            _backendProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "dotnet",
                    Arguments = args,
                    WorkingDirectory = dir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            _backendProcess.OutputDataReceived += (s, e) => {
                if (e.Data == null) return;
                Log("[Backend] " + e.Data);
                if (e.Data.Contains("error CS") || e.Data.Contains("fail:") || e.Data.Contains("crit:")) BackendHasError = true;
            };
            
            _backendProcess.Start();
            _backendProcess.BeginOutputReadLine();
            Log($"Started Backend on port {BackendRunningPort}");
        } catch (Exception ex) { Log("Error starting backend: " + ex.Message); }
    }

    public static void StartFrontend() {
        try {
            FrontendHasError = false;
            string dir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "frontend"));
            FrontendRunningPort = RefreshFrontendPort();
            KillProcessOnPort(FrontendRunningPort);

            _frontendProcess = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "cmd.exe",
                    Arguments = "/c npm run dev",
                    WorkingDirectory = dir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            _frontendProcess.OutputDataReceived += (s, e) => {
                if (e.Data == null) return;
                Log("[Vite] " + e.Data);
                if (e.Data.Contains("failed to compile", StringComparison.OrdinalIgnoreCase)) FrontendHasError = true;
            };
            
            _frontendProcess.Start();
            _frontendProcess.BeginOutputReadLine();
            Log($"Started Frontend on port {FrontendRunningPort}");
        } catch (Exception ex) { Log("Error starting frontend: " + ex.Message); }
    }

    public static void StopBackend() {
        if (_backendProcess != null) { KillProcessTree(_backendProcess); _backendProcess = null; }
        else KillProcessOnPort(RefreshBackendPort());
    }

    public static void StopFrontend() {
        if (_frontendProcess != null) { KillProcessTree(_frontendProcess); _frontendProcess = null; }
        else KillProcessOnPort(RefreshFrontendPort());
    }

    public static void StartAll() { StartBackend(); StartFrontend(); }
    public static void StopAll() { StopBackend(); StopFrontend(); }
    public static void RestartAll() { StopAll(); Task.Delay(1000).ContinueWith(_ => StartAll()); }

    public static void ToggleGodMode() {
        try {
            string d = Path.GetDirectoryName(GodModePath);
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            if (File.Exists(GodModePath)) { File.Delete(GodModePath); Log("God Mode disabled."); }
            else { File.WriteAllText(GodModePath, "enabled"); Log("God Mode enabled."); }
        } catch (Exception e) { Log("Error toggling God Mode: " + e.Message); }
    }

    public static void CheckExternalServers() {
        BackendRunningPort = RefreshBackendPort();
        FrontendRunningPort = RefreshFrontendPort();
    }

    private static void KillProcessTree(Process p) {
        if (p == null || p.HasExited) return;
        try { Process.Start(new ProcessStartInfo { FileName = "taskkill", Arguments = $"/F /T /PID {p.Id}", CreateNoWindow = true })?.WaitForExit(); } catch {}
    }

    private static void KillProcessOnPort(int port) {
        try {
            var proc = new Process { StartInfo = new ProcessStartInfo { FileName = "cmd", Arguments = $"/c netstat -ano | findstr :{port}", RedirectStandardOutput = true, CreateNoWindow = true } };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            foreach (var line in output.Split('\n')) {
                if (line.Contains("LISTENING")) {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (int.TryParse(parts[^1], out int pid)) {
                        Process.Start(new ProcessStartInfo { FileName = "taskkill", Arguments = $"/F /T /PID {pid}", CreateNoWindow = true })?.WaitForExit();
                    }
                }
            }
        } catch {}
    }

    public static Task<bool> IsPortActiveAsync(string host, int port) {
        _ = host;
        try {
            var properties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var listeners = properties.GetActiveTcpListeners();
            return Task.FromResult(listeners.Any(l => l.Port == port));
        } catch {}
        return Task.FromResult(false);
    }

    public static string GetRootDirectory() {
        string b = AppDomain.CurrentDomain.BaseDirectory;
        string c = b;
        while (!string.IsNullOrEmpty(c)) {
            if (Path.GetFileName(c).Equals("backend", StringComparison.OrdinalIgnoreCase)) return Path.GetDirectoryName(c);
            if (Directory.Exists(Path.Combine(c, "backend"))) return c;
            c = Path.GetDirectoryName(c);
        }
        return b;
    }

    private static string GetLocalIp() {
        try {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            return (s.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        } catch { return "127.0.0.1"; }
    }

    public static bool IsValidId(string id) => !string.IsNullOrEmpty(id) && id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    public static string[] LoadCompanySyncUrls(string id) {
        string p = Path.Combine(GetRootDirectory(), "backend", "assets", id, id + "_config.json");
        if (!File.Exists(p)) return [];
        try {
            using var doc = JsonDocument.Parse(File.ReadAllText(p));
            if (doc.RootElement.TryGetProperty("sync_urls", out var urls))
                return urls.EnumerateObject().Select(x => x.Value.GetString()).ToArray();
        } catch {}
        return [];
    }

    public static void WriteCompanyConfig(string dir, string id, string urlsText) {
        var dict = new Dictionary<string, string>();
        foreach (var url in urlsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
            string t = url.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            string key = string.Concat(Path.GetFileNameWithoutExtension(t).Where(c => char.IsLetterOrDigit(c) || c == '_'));
            if (!string.IsNullOrEmpty(key) && char.IsLetter(key[0])) dict[key] = t;
        }
        File.WriteAllText(Path.Combine(dir, id + "_config.json"), JsonSerializer.Serialize(new { sync_urls = dict }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void TriggerDatabaseSync(string id) {
        Task.Run(() => {
            try {
                Log($"[Sync] Starting database sync for {id}...");
                var p = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = "dotnet",
                        Arguments = "run -- --sync " + id,
                        WorkingDirectory = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend")),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                p.OutputDataReceived += (s, e) => { if (e.Data != null) Log("[Sync] " + e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) Log("[Sync Error] " + e.Data); };
                p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
                Log($"[Sync] Finished for {id} with code {p.ExitCode}");
            } catch (Exception ex) { Log("[Sync Error] " + ex.Message); }
        });
    }
}

public class PortalCompanySaveRequest {
    public string CompanyId { get; set; }
    public string Mode { get; set; }
    public string SyncUrls { get; set; }
}
