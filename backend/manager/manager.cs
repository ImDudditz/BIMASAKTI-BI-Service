using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using BimasaktiReports.FinancialReports.Backend.Engines;

namespace BimasaktiReports.FinancialReports.Manager
{
    public enum ServerStatus
    {
        Stopped,
        Loading,
        Running,
        Error
    }

    public class ManagerServer
    {
        // Core Process Management
        private static Process _backendProcess;
        private static Process _frontendProcess;
        private static bool _backendHasError;
        private static bool _frontendHasError;
        
        // Log buffer for the live terminal log viewer
        private static readonly ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private const int MaxLogLines = 1000;

        private static string _godModePath;
        private static string _localIp;
        private static string _coolAlias;

        public static void Main(string[] args)
        {
            // Auto-detect IPs & Hostname
            _localIp = GetLocalIp();
            string hostname = Dns.GetHostName();
            _coolAlias = hostname.ToLowerInvariant() + ".local";
            
            // God Mode file path: backend/engines/.god_mode_enabled
            _godModePath = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend", "engines", ".god_mode_enabled"));

            Log("Bimasakti BI Service Web Manager Initializing...");
            Log("Detected Network IP: " + _localIp);
            Log("Active Alias: " + _coolAlias);

            var builder = WebApplication.CreateBuilder(args);

            // Add standard services
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("ManagerCorsPolicy", policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            var app = builder.Build();

            app.UseCors("ManagerCorsPolicy");
            
            // Serve static files from wwwroot
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Check if backend/frontend are already running externally on ports 8001/5173
            CheckExternalServers();

            // --- API ENDPOINTS ---

            // 1. Get process and server status
            app.MapGet("/api/status", () =>
            {
                bool backendPortActive = IsPortActive("127.0.0.1", 8001);
                bool frontendPortActive = IsPortActive("127.0.0.1", 5173);

                bool backendProcessActive = (_backendProcess != null && !_backendProcess.HasExited) || backendPortActive;
                bool frontendProcessActive = (_frontendProcess != null && !_frontendProcess.HasExited) || frontendPortActive;

                ServerStatus backendStatus = ServerStatus.Stopped;
                if (_backendHasError)
                {
                    backendStatus = ServerStatus.Error;
                }
                else if (backendPortActive)
                {
                    backendStatus = ServerStatus.Running;
                }
                else if (_backendProcess != null && !_backendProcess.HasExited)
                {
                    backendStatus = ServerStatus.Loading;
                }
                else if (_backendProcess != null && _backendProcess.HasExited)
                {
                    backendStatus = ServerStatus.Error;
                }

                ServerStatus frontendStatus = ServerStatus.Stopped;
                if (_frontendHasError)
                {
                    frontendStatus = ServerStatus.Error;
                }
                else if (frontendPortActive)
                {
                    frontendStatus = ServerStatus.Running;
                }
                else if (_frontendProcess != null && !_frontendProcess.HasExited)
                {
                    frontendStatus = ServerStatus.Loading;
                }
                else if (_frontendProcess != null && _frontendProcess.HasExited)
                {
                    frontendStatus = ServerStatus.Error;
                }

                return Results.Ok(new
                {
                    backendActive = backendProcessActive,
                    frontendActive = frontendProcessActive,
                    backendStatus = backendStatus.ToString(),
                    frontendStatus = frontendStatus.ToString(),
                    godMode = File.Exists(_godModePath),
                    localIp = _localIp,
                    coolAlias = _coolAlias
                });
            });

            // 2. Start servers
            app.MapPost("/api/start", () =>
            {
                StartAll();
                return Results.Ok(new { message = "Server startup sequence triggered." });
            });

            // 3. Stop servers
            app.MapPost("/api/stop", () =>
            {
                StopAll();
                return Results.Ok(new { message = "Server shutdown sequence triggered." });
            });

            // 4. Restart servers
            app.MapPost("/api/restart", () =>
            {
                RestartAll();
                return Results.Ok(new { message = "Server restart sequence triggered." });
            });

            // 5. Toggle God Mode
            app.MapPost("/api/godmode", () =>
            {
                ToggleGodMode();
                return Results.Ok(new { godMode = File.Exists(_godModePath) });
            });

            // 6. Get consolidated system logs
            app.MapGet("/api/logs", () =>
            {
                return Results.Ok(_logQueue.ToArray());
            });

            // 7. Clear consolidated system logs
            app.MapPost("/api/logs/clear", () =>
            {
                while (_logQueue.TryDequeue(out _)) { }
                Log("Terminal log console cleared.");
                return Results.Ok(new { message = "Logs cleared." });
            });

            // 8. List registered companies
            app.MapGet("/api/companies", () =>
            {
                var list = new List<string>();
                try
                {
                    string assetsDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend", "assets"));
                    if (Directory.Exists(assetsDir))
                    {
                        string[] dirs = Directory.GetDirectories(assetsDir);
                        foreach (string dir in dirs)
                        {
                            string name = Path.GetFileName(dir);
                            if (name.Length == 5 && IsValidId(name))
                            {
                                list.Add(name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("Error listing companies: " + ex.Message);
                }
                return Results.Ok(list);
            });

            // 8.1 List users for a company (excluding admin role)
            app.MapGet("/api/manager/companies/{companyId}/users", async (string companyId) =>
            {
                string id = companyId.Trim().ToUpperInvariant();
                if (id.Length != 5 || !IsValidId(id))
                {
                    return Results.BadRequest(new { error = "Invalid Company ID." });
                }

                string databasePath = svcDbUtils.GetSafeDbPath(id);
                try
                {
                    using (var dbContext = new TenantDbContext(databasePath))
                    {
                        await dbContext.Database.EnsureCreatedAsync();
                        var usersList = await dbContext.Users
                            .Where(u => u.CompanyId == id && u.Role != "admin")
                            .ToListAsync();
                        
                        return Results.Ok(usersList.Select(u => new { id = u.Id, username = u.Username, role = u.Role }));
                    }
                }
                catch (Exception ex)
                {
                    Log("Error listing users for company " + id + ": " + ex.Message);
                    return Results.StatusCode(500);
                }
            });

            // 8.2 Get permissions for a specific user in a company
            app.MapGet("/api/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId) =>
            {
                string id = companyId.Trim().ToUpperInvariant();
                if (id.Length != 5 || !IsValidId(id))
                {
                    return Results.BadRequest(new { error = "Invalid Company ID." });
                }

                string databasePath = svcDbUtils.GetSafeDbPath(id);
                try
                {
                    using (var dbContext = new TenantDbContext(databasePath))
                    {
                        await dbContext.Database.EnsureCreatedAsync();
                        
                        var targetUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == id);
                        if (targetUser == null)
                        {
                            return Results.NotFound(new { error = "User not found." });
                        }

                        var widgetsList = await dbContext.UserWidgets.Where(w => w.UserId == userId).ToListAsync();
                        var reportsList = await dbContext.UserReports.Where(r => r.UserId == userId).ToListAsync();

                        return Results.Ok(new
                        {
                            widgets = widgetsList.ToDictionary(w => w.WidgetKey, w => w.IsActive),
                            reports = reportsList.ToDictionary(r => r.ReportKey, r => r.IsActive)
                        });
                    }
                }
                catch (Exception ex)
                {
                    Log("Error loading permissions for user " + userId + " in company " + id + ": " + ex.Message);
                    return Results.StatusCode(500);
                }
            });

            // 8.3 Save permissions for a user in a company
            app.MapPost("/api/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId, PermissionSettingsSpecification permissions) =>
            {
                string id = companyId.Trim().ToUpperInvariant();
                if (id.Length != 5 || !IsValidId(id))
                {
                    return Results.BadRequest(new { error = "Invalid Company ID." });
                }

                if (permissions == null)
                {
                    return Results.BadRequest(new { error = "Permissions body cannot be null." });
                }

                string databasePath = svcDbUtils.GetSafeDbPath(id);
                try
                {
                    using (var dbContext = new TenantDbContext(databasePath))
                    {
                        await dbContext.Database.EnsureCreatedAsync();

                        var targetUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == id);
                        if (targetUser == null)
                        {
                            return Results.NotFound(new { error = "User not found." });
                        }

                        // Save widgets
                        var existingWidgets = await dbContext.UserWidgets.Where(w => w.UserId == userId).ToListAsync();
                        var existingWidgetsMap = existingWidgets.ToDictionary(w => w.WidgetKey);

                        foreach (var kvp in permissions.Widgets)
                        {
                            if (existingWidgetsMap.TryGetValue(kvp.Key, out var w))
                            {
                                w.IsActive = kvp.Value;
                            }
                            else
                            {
                                dbContext.UserWidgets.Add(new UserWidget { UserId = userId, WidgetKey = kvp.Key, IsActive = kvp.Value });
                            }
                        }

                        // Save reports
                        var existingReports = await dbContext.UserReports.Where(r => r.UserId == userId).ToListAsync();
                        var existingReportsMap = existingReports.ToDictionary(r => r.ReportKey);

                        foreach (var kvp in permissions.Reports)
                        {
                            if (existingReportsMap.TryGetValue(kvp.Key, out var r))
                            {
                                r.IsActive = kvp.Value;
                            }
                            else
                            {
                                dbContext.UserReports.Add(new UserReport { UserId = userId, ReportKey = kvp.Key, IsActive = kvp.Value });
                            }
                        }

                        await dbContext.SaveChangesAsync();
                        Log($"Saved permissions successfully for user: {targetUser.Username} in company: {id}");
                        return Results.Ok(new { message = "Permissions updated successfully." });
                    }
                }
                catch (Exception ex)
                {
                    Log("Error saving permissions for user " + userId + " in company " + id + ": " + ex.Message);
                    return Results.StatusCode(500);
                }
            });

            // 9. Load company configuration sync URLs
            app.MapGet("/api/companies/{id}", (string id) =>
            {
                string companyId = id.Trim().ToUpperInvariant();
                if (companyId.Length != 5 || !IsValidId(companyId))
                {
                    return Results.BadRequest(new { error = "Invalid Company ID. Must be exactly 5 alphanumeric characters." });
                }

                string[] urls = LoadCompanySyncUrls(companyId);
                return Results.Ok(new { companyId, urls });
            });

            // 10. Register or modify company config
            app.MapPost("/api/companies", (CompanySaveRequest req) =>
            {
                if (req == null) return Results.BadRequest(new { error = "Request body is empty." });

                string companyId = req.CompanyId?.Trim()?.ToUpperInvariant();
                string mode = req.Mode?.Trim() ?? "New";
                string syncUrlsText = req.SyncUrls ?? "";

                if (string.IsNullOrEmpty(companyId))
                {
                    return Results.BadRequest(new { error = "Company ID must be provided." });
                }

                if (companyId.Length != 5)
                {
                    return Results.BadRequest(new { error = "Company ID must be exactly 5 characters long." });
                }

                if (!IsValidId(companyId))
                {
                    return Results.BadRequest(new { error = "Company ID must contain only alphanumeric characters, dashes, or underscores." });
                }

                string rootDir = GetRootDirectory();
                string assetsDir = Path.GetFullPath(Path.Combine(rootDir, "backend", "assets"));
                string tmplDir = Path.GetFullPath(Path.Combine(assetsDir, "BMS"));
                string targetDir = Path.GetFullPath(Path.Combine(assetsDir, companyId));

                if (mode == "New")
                {
                    if (string.IsNullOrEmpty(syncUrlsText.Trim()))
                    {
                        return Results.BadRequest(new { error = "Sync URLs cannot be empty when registering a new company." });
                    }

                    if (!Directory.Exists(tmplDir))
                    {
                        return Results.BadRequest(new { error = "Template directory (BMS) not found under: " + tmplDir });
                    }

                    if (Directory.Exists(targetDir))
                    {
                        return Results.BadRequest(new { error = "Company directory already exists: " + targetDir });
                    }

                    try
                    {
                        Log("Creating new tenant: " + companyId + " from template: BMS");
                        Directory.CreateDirectory(targetDir);
                        
                        foreach (string dirPath in Directory.GetDirectories(tmplDir, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(tmplDir, targetDir));
                        }

                        string tmplSyncPath = Path.Combine(tmplDir, "BMS_sync.py");
                        if (File.Exists(tmplSyncPath))
                        {
                            string targetSyncPath = Path.Combine(targetDir, companyId + "_sync.py");
                            File.Copy(tmplSyncPath, targetSyncPath, true);

                            string content = File.ReadAllText(targetSyncPath);
                            if (content.Contains("BMS"))
                            {
                                content = content.Replace("BMS", companyId);
                            }
                            string lowerTmpl = "bms";
                            string lowerNewId = companyId.ToLowerInvariant();
                            if (content.Contains(lowerTmpl))
                            {
                                content = content.Replace(lowerTmpl, lowerNewId);
                            }
                            File.WriteAllText(targetSyncPath, content);
                        }
                        else
                        {
                            Log("Warning: Template sync script not found: " + tmplSyncPath);
                        }

                        WriteCompanyConfig(targetDir, companyId, syncUrlsText);
                        Log("Success: Company " + companyId + " created and configuration saved!");
                        TriggerDatabaseSync(companyId);

                        return Results.Ok(new { message = $"Company {companyId} created and registered successfully!" });
                    }
                    catch (Exception ex)
                    {
                        string errMsg = "Error creating company: " + ex.Message;
                        Log(errMsg);
                        return Results.StatusCode(500);
                    }
                }
                else // Modify
                {
                    if (!Directory.Exists(targetDir))
                    {
                        return Results.BadRequest(new { error = "Target company directory not found: " + targetDir });
                    }

                    try
                    {
                        Log("Modifying configuration for company: " + companyId);
                        WriteCompanyConfig(targetDir, companyId, syncUrlsText);
                        Log("Success: Company " + companyId + " configuration updated!");
                        TriggerDatabaseSync(companyId);

                        return Results.Ok(new { message = $"Company {companyId} configuration updated successfully!" });
                    }
                    catch (Exception ex)
                    {
                        string errMsg = "Error modifying company: " + ex.Message;
                        Log(errMsg);
                        return Results.StatusCode(500);
                    }
                }
            });

            // 11. Trigger sync explicitly
            app.MapPost("/api/companies/{id}/sync", (string id) =>
            {
                string companyId = id.Trim().ToUpperInvariant();
                if (companyId.Length != 5 || !IsValidId(companyId))
                {
                    return Results.BadRequest(new { error = "Invalid Company ID." });
                }

                TriggerDatabaseSync(companyId);
                return Results.Ok(new { message = $"Database sync initiated for {companyId}." });
            });

            // Auto-launch browser to port 5000 once ASP.NET server is ready
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                try
                {
                    Log("Opening Web Manager in default browser...");
                    Process.Start(new ProcessStartInfo("http://localhost:5000") { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Log("Could not launch web browser automatically: " + ex.Message + ". Please open http://localhost:5000 manually.");
                }
            });

            app.Run("http://localhost:5000");
        }

        // --- CORE PROCESS CONTROL ---

        private static string GetLocalIp()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 80);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint != null ? endPoint.Address.ToString() : "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private static void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{timestamp}] {message}";
            Console.WriteLine(line);
            
            _logQueue.Enqueue(line);
            while (_logQueue.Count > MaxLogLines)
            {
                _logQueue.TryDequeue(out _);
            }
        }

        private static void StartBackend()
        {
            try
            {
                _backendHasError = false;
                string backendDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend"));

                if (!Directory.Exists(backendDir))
                {
                    Log($"Error: Backend directory not found: {backendDir}");
                    return;
                }

                _backendProcess = new Process();
                _backendProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run",
                    WorkingDirectory = backendDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _backendProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        Log("[Backend] " + e.Data);
                        if (e.Data.IndexOf(": error CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Build FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Unhandled exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("fail:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("crit:", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _backendHasError = true;
                        }
                    }
                };

                _backendProcess.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        Log("[Backend Error] " + e.Data);
                        if (e.Data.IndexOf(": error CS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Build FAILED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Unhandled exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("System.Exception", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _backendHasError = true;
                        }
                    }
                };

                _backendProcess.Start();
                _backendProcess.BeginOutputReadLine();
                _backendProcess.BeginErrorReadLine();

                Log("Starting C# Web API Backend on port 8001...");
            }
            catch (Exception ex)
            {
                Log("Failed to start C# backend: " + ex.Message);
            }
        }

        private static void StopBackend()
        {
            if (_backendProcess != null)
            {
                Log("Stopping C# Backend process tree...");
                KillProcessTree(_backendProcess);
                _backendProcess.Dispose();
                _backendProcess = null;
                Log("C# Backend stopped.");
            }
            else
            {
                KillProcessOnPort(8001);
            }
        }

        private static void StartFrontend()
        {
            try
            {
                _frontendHasError = false;
                string frontendDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "frontend"));

                if (!Directory.Exists(frontendDir))
                {
                    Log($"Error: Frontend directory not found: {frontendDir}");
                    return;
                }

                _frontendProcess = new Process();
                _frontendProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c npm run dev",
                    WorkingDirectory = frontendDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                _frontendProcess.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        Log("[Vite] " + e.Data);
                        if (e.Data.IndexOf("failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Syntax Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("SyntaxError", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Internal server error", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _frontendHasError = true;
                        }
                    }
                };

                _frontendProcess.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        Log("[Vite Error] " + e.Data);
                        if (e.Data.IndexOf("failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Failed to compile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Syntax Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("SyntaxError", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("Internal server error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            e.Data.IndexOf("ERROR  ", StringComparison.Ordinal) >= 0 ||
                            e.Data.IndexOf("error: ", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _frontendHasError = true;
                        }
                    }
                };

                _frontendProcess.Start();
                _frontendProcess.BeginOutputReadLine();
                _frontendProcess.BeginErrorReadLine();

                Log("Starting Vue Frontend on port 5173...");
            }
            catch (Exception ex)
            {
                Log("Failed to start Vue Frontend: " + ex.Message);
            }
        }

        private static void StopFrontend()
        {
            if (_frontendProcess != null)
            {
                Log("Stopping Vue Frontend process tree...");
                KillProcessTree(_frontendProcess);
                _frontendProcess.Dispose();
                _frontendProcess = null;
                Log("Vue Frontend stopped.");
            }
            else
            {
                KillProcessOnPort(5173);
            }
        }

        private static void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited) return;

            try
            {
                using (Process killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /T /PID " + process.Id,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }))
                {
                    if (killer != null) killer.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Log("Error executing process tree shutdown: " + ex.Message);
            }
        }

        // --- GOD MODE MANAGEMENT ---

        private static void ToggleGodMode()
        {
            try
            {
                string parentDir = Path.GetDirectoryName(_godModePath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                if (File.Exists(_godModePath))
                {
                    File.Delete(_godModePath);
                    Log("God Mode disabled.");
                }
                else
                {
                    File.WriteAllText(_godModePath, "enabled");
                    Log("God Mode enabled! Admin bypass active.");
                }
            }
            catch (Exception ex)
            {
                Log("Error toggling God Mode: " + ex.Message);
            }
        }

        // --- TENANT HELPERS ---

        private static string[] LoadCompanySyncUrls(string companyId)
        {
            string configPath = Path.Combine(GetRootDirectory(), "backend", "assets", companyId, companyId + "_config.json");
            if (!File.Exists(configPath)) return new string[0];
            
            try
            {
                string content = File.ReadAllText(configPath);
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"""[^""]+""\s*:\s*""([^""]+)""");
                var list = new List<string>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string url = match.Groups[1].Value;
                        url = url.Replace("\\\"", "\"").Replace("\\\\", "\\");
                        list.Add(url);
                    }
                }
                return list.ToArray();
            }
            catch (Exception ex)
            {
                Log("Error loading sync URLs: " + ex.Message);
                return new string[0];
            }
        }

        private static void WriteCompanyConfig(string dir, string companyId, string syncUrlsText)
        {
            string[] urls = syncUrlsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("    \"sync_urls\": {");

            bool first = true;
            foreach (string url in urls)
            {
                string trimmedUrl = url.Trim();
                if (string.IsNullOrEmpty(trimmedUrl)) continue;

                string rawKey = Path.GetFileNameWithoutExtension(trimmedUrl);
                string key = SanitizeKey(rawKey);
                if (string.IsNullOrEmpty(key))
                {
                    Log("Warning: Skipping URL due to invalid/unsanitizable key: " + trimmedUrl);
                    continue;
                }

                string escapedUrl = trimmedUrl.Replace("\\", "\\\\").Replace("\"", "\\\"");

                if (!first) sb.AppendLine(",");
                sb.Append("        \"" + key + "\": \"" + escapedUrl + "\"");
                first = false;
            }
            if (!first) sb.AppendLine();
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string configPath = Path.Combine(dir, companyId + "_config.json");
            File.WriteAllText(configPath, sb.ToString());
        }

        private static void TriggerDatabaseSync(string companyId)
        {
            Task.Run(() =>
            {
                try
                {
                    Log("[Sync] Launching database synchronization for " + companyId + "...");
                    string backendDir = Path.GetFullPath(Path.Combine(GetRootDirectory(), "backend"));

                    using (Process syncProcess = new Process())
                    {
                        syncProcess.StartInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = "run -- --sync " + companyId,
                            WorkingDirectory = backendDir,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        syncProcess.OutputDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log("[Sync] " + e.Data);
                            }
                        };

                        syncProcess.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log("[Sync Error] " + e.Data);
                            }
                        };

                        syncProcess.Start();
                        syncProcess.BeginOutputReadLine();
                        syncProcess.BeginErrorReadLine();
                        syncProcess.WaitForExit();

                        if (syncProcess.ExitCode == 0)
                        {
                            Log("[Sync] Database sync completed successfully for " + companyId + "!");
                        }
                        else
                        {
                            Log("[Sync] Database sync failed with exit code: " + syncProcess.ExitCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("[Sync] Critical error during sync execution: " + ex.Message);
                }
            });
        }

        private static void CheckExternalServers()
        {
            bool backRunning = IsPortActive("127.0.0.1", 8001);
            bool frontRunning = IsPortActive("127.0.0.1", 5173);

            if (backRunning)
            {
                Log("[System] Detected C# Backend is already running externally on port 8001.");
            }
            if (frontRunning)
            {
                Log("[System] Detected Vue Frontend is already running externally on port 5173.");
            }
        }

        private static void StartAll()
        {
            StartBackend();
            StartFrontend();
        }

        private static void StopAll()
        {
            StopBackend();
            StopFrontend();
        }

        private static void RestartAll()
        {
            Log("Restarting all servers...");
            StopAll();
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                StartAll();
            });
        }

        // --- UTILITIES ---

        private static bool IsPortActive(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(host, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(150, false); // 150ms timeout
                    if (success)
                    {
                        client.EndConnect(result);
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void KillProcessOnPort(int port)
        {
            try
            {
                string output;
                using (Process netstat = new Process())
                {
                    netstat.StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c netstat -ano | findstr :" + port,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    netstat.Start();
                    output = netstat.StandardOutput.ReadToEnd();
                    netstat.WaitForExit();
                }

                if (string.IsNullOrEmpty(output)) return;

                string[] lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    if (line.Contains("LISTENING"))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            string pidStr = parts[parts.Length - 1].Trim();
                            int pid;
                            if (int.TryParse(pidStr, out pid) && pid > 0)
                            {
                                Log($"Found external process ID {pid} on port {port}. Terminating...");
                                using (Process killer = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = "/F /T /PID " + pid,
                                    CreateNoWindow = true,
                                    UseShellExecute = false
                                }))
                                {
                                    if (killer != null) killer.WaitForExit(3000);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error terminating process on port {port}: " + ex.Message);
            }
        }

        private static string GetRootDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string current = baseDir;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(Path.Combine(current, "backend")) && Directory.Exists(Path.Combine(current, "frontend")))
                {
                    return current;
                }
                string parent = Path.GetDirectoryName(current);
                if (parent == current || string.IsNullOrEmpty(parent)) break;
                current = parent;
            }
            return baseDir;
        }

        private static bool IsValidId(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            foreach (char c in input)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
            return true;
        }

        private static string SanitizeKey(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
            }
            string result = sb.ToString();
            if (result.Length > 0 && !char.IsLetter(result[0]))
            {
                return "";
            }
            return result;
        }
    }

    public class CompanySaveRequest
    {
        public string CompanyId { get; set; }
        public string Mode { get; set; }
        public string SyncUrls { get; set; }
    }
}
