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

            // --- API ENDPOINTS REGISTRATION ---
            app.MapManagerEndpoints();

            app.Run("http://localhost:5050");
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

        public static void Log(string message, bool writeToConsole = true)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{timestamp}] {message}";
            if (writeToConsole)
            {
                Console.WriteLine(line);
            }
            
            _logQueue.Enqueue(line);
            while (_logQueue.Count > MaxLogLines)
            {
                _logQueue.TryDequeue(out _);
            }
        }

        public static void StartBackend()
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

                // Ensure port 8001 is completely free to prevent restart collisions
                KillProcessOnPort(8001);

                // Use pre-compiled DLL if present for near-instant boot, fallback to dotnet run
                string dllPath = Path.Combine(backendDir, "bin", "Debug", "net8.0", "BimasaktiReports.FinancialReports.Backend.dll");
                bool useDll = File.Exists(dllPath);
                string executableArguments = useDll ? $"\"{dllPath}\"" : "run";

                _backendProcess = new Process();
                _backendProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = executableArguments,
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
                        Log("[Backend] " + e.Data, true);
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
                        Log("[Backend Error] " + e.Data, true);
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

        public static void StopBackend()
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

        public static void StartFrontend()
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

                // Ensure port 5173 is completely free to prevent restart collisions
                KillProcessOnPort(5173);

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
                        Log("[Vite] " + e.Data, true);
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
                        Log("[Vite Error] " + e.Data, true);
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

        public static void StopFrontend()
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

        public static void ToggleGodMode()
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

        public static string[] LoadCompanySyncUrls(string companyId)
        {
            string configPath = Path.Combine(GetRootDirectory(), "backend", "assets", companyId, companyId + "_config.json");
            if (!File.Exists(configPath)) return Array.Empty<string>();
            
            try
            {
                string content = File.ReadAllText(configPath);
                using (JsonDocument doc = JsonDocument.Parse(content))
                {
                    if (doc.RootElement.TryGetProperty("sync_urls", out var syncUrlsProp) && syncUrlsProp.ValueKind == JsonValueKind.Object)
                    {
                        var list = new List<string>();
                        foreach (var prop in syncUrlsProp.EnumerateObject())
                        {
                            list.Add(prop.Value.GetString());
                        }
                        return list.ToArray();
                    }
                }
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                Log("Error loading sync URLs: " + ex.Message);
                return Array.Empty<string>();
            }
        }

        public static void WriteCompanyConfig(string dir, string companyId, string syncUrlsText)
        {
            string[] urls = syncUrlsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var syncUrlsDict = new Dictionary<string, string>();
            
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

                syncUrlsDict[key] = trimmedUrl;
            }

            var configObj = new { sync_urls = syncUrlsDict };
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(configObj, options);

            string configPath = Path.Combine(dir, companyId + "_config.json");
            File.WriteAllText(configPath, json);
        }

        public static void TriggerDatabaseSync(string companyId)
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
                                Log("[Sync] " + e.Data, false);
                            }
                        };

                        syncProcess.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                Log("[Sync Error] " + e.Data, false);
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
            bool backRunning = IsPortActiveAsync("127.0.0.1", 8001).GetAwaiter().GetResult();
            bool frontRunning = IsPortActiveAsync("127.0.0.1", 5173).GetAwaiter().GetResult();

            if (backRunning)
            {
                Log("[System] Detected C# Backend is already running externally on port 8001.");
            }
            if (frontRunning)
            {
                Log("[System] Detected Vue Frontend is already running externally on port 5173.");
            }
        }

        public static void StartAll()
        {
            StartBackend();
            StartFrontend();
        }

        public static void StopAll()
        {
            StopBackend();
            StopFrontend();
        }

        public static void RestartAll()
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

        public static async Task<bool> IsPortActiveAsync(string host, int port)
        {
            if (await TryConnectAsync(host, port)) return true;
            if (host == "127.0.0.1" && await TryConnectAsync("localhost", port)) return true;
            return false;
        }

        private static async Task<bool> TryConnectAsync(string host, int port)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(host, port);
                    var delayTask = Task.Delay(500); // 500ms timeout provides robust Windows loopback resolution
                    var completedTask = await Task.WhenAny(connectTask, delayTask);
                    if (completedTask == connectTask)
                    {
                        await connectTask; // propagate exceptions if any
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

        public static string GetRootDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string current = baseDir;
            
            // 1. Walk up from BaseDirectory to find "backend" or its parent containing it
            while (!string.IsNullOrEmpty(current))
            {
                string dirName = Path.GetFileName(current);
                if (dirName.Equals("backend", StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(current);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        return parent;
                    }
                    break;
                }
                if (Directory.Exists(Path.Combine(current, "backend")))
                {
                    return current;
                }
                string parentDir = Path.GetDirectoryName(current);
                if (parentDir == current || string.IsNullOrEmpty(parentDir)) break;
                current = parentDir;
            }

            // 2. Fallback: Walk up from Current Working Directory
            current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrEmpty(current))
            {
                string dirName = Path.GetFileName(current);
                if (dirName.Equals("backend", StringComparison.OrdinalIgnoreCase))
                {
                    string parent = Path.GetDirectoryName(current);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        return parent;
                    }
                    break;
                }
                if (Directory.Exists(Path.Combine(current, "backend")))
                {
                    return current;
                }
                string parentDir = Path.GetDirectoryName(current);
                if (parentDir == current || string.IsNullOrEmpty(parentDir)) break;
                current = parentDir;
            }

            // 3. Last resort fallback: Go up 4 levels from baseDir
            try
            {
                string relativeFallback = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
                if (Directory.Exists(Path.Combine(relativeFallback, "backend")))
                {
                    return relativeFallback;
                }
            }
            catch { }

            return baseDir;
        }

        public static bool IsValidId(string input)
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

        // Global Helper for Company ID & Request Body Validations
        public static (bool IsValid, string ErrorMessage) ValidateCompanyId(string rawId)
        {
            string cleanId = rawId?.Trim()?.ToUpperInvariant();
            if (string.IsNullOrEmpty(cleanId))
            {
                return (false, "Company ID must be provided.");
            }
            if (cleanId.Length != 5)
            {
                return (false, "Company ID must be exactly 5 characters long.");
            }
            if (!IsValidId(cleanId))
            {
                return (false, "Company ID must contain only alphanumeric characters, dashes, or underscores.");
            }
            return (true, null);
        }

        // Global properties/getters for API routing
        public static string GodModePath => _godModePath;
        public static string LocalIp => _localIp;
        public static string CoolAlias => _coolAlias;
        public static bool BackendHasError => _backendHasError;
        public static bool FrontendHasError => _frontendHasError;
        public static ConcurrentQueue<string> LogQueue => _logQueue;
    }

    public static class ManagerEndpointExtensions
    {
        public static void MapManagerEndpoints(this WebApplication app)
        {
            // 1. Get process and server status (Refactored to be Async & Non-blocking)
            app.MapGet("/api/status", async () =>
            {
                bool backendPortActive = await ManagerServer.IsPortActiveAsync("127.0.0.1", 8001);
                bool frontendPortActive = await ManagerServer.IsPortActiveAsync("127.0.0.1", 5173);

                bool backendProcessActive = backendPortActive; // simplified as port active indicates running status
                bool frontendProcessActive = frontendPortActive;

                ServerStatus backendStatus = ServerStatus.Stopped;
                if (ManagerServer.BackendHasError)
                {
                    backendStatus = ServerStatus.Error;
                }
                else if (backendPortActive)
                {
                    backendStatus = ServerStatus.Running;
                }

                ServerStatus frontendStatus = ServerStatus.Stopped;
                if (ManagerServer.FrontendHasError)
                {
                    frontendStatus = ServerStatus.Error;
                }
                else if (frontendPortActive)
                {
                    frontendStatus = ServerStatus.Running;
                }

                return Results.Ok(new
                {
                    backendActive = backendProcessActive,
                    frontendActive = frontendProcessActive,
                    backendStatus = backendStatus.ToString(),
                    frontendStatus = frontendStatus.ToString(),
                    godMode = File.Exists(ManagerServer.GodModePath),
                    localIp = ManagerServer.LocalIp,
                    coolAlias = ManagerServer.CoolAlias
                });
            });

            // 2. Start servers
            app.MapPost("/api/start", () =>
            {
                ManagerServer.StartAll();
                return Results.Ok(new { message = "Server startup sequence triggered." });
            });

            // 3. Stop servers
            app.MapPost("/api/stop", () =>
            {
                ManagerServer.StopAll();
                return Results.Ok(new { message = "Server shutdown sequence triggered." });
            });

            // 4. Restart servers
            app.MapPost("/api/restart", () =>
            {
                ManagerServer.RestartAll();
                return Results.Ok(new { message = "Server restart sequence triggered." });
            });

            // 5. Toggle God Mode
            app.MapPost("/api/godmode", () =>
            {
                ManagerServer.ToggleGodMode();
                return Results.Ok(new { godMode = File.Exists(ManagerServer.GodModePath) });
            });

            // 6. Get consolidated system logs
            app.MapGet("/api/logs", () =>
            {
                return Results.Ok(ManagerServer.LogQueue.ToArray());
            });

            // 7. Clear consolidated system logs
            app.MapPost("/api/logs/clear", () =>
            {
                while (ManagerServer.LogQueue.TryDequeue(out _)) { }
                ManagerServer.Log("Terminal log console cleared.");
                return Results.Ok(new { message = "Logs cleared." });
            });

            // 8. List registered companies
            app.MapGet("/api/companies", () =>
            {
                var list = new List<string>();
                try
                {
                    string assetsDir = Path.GetFullPath(Path.Combine(ManagerServer.GetRootDirectory(), "backend", "assets"));
                    if (Directory.Exists(assetsDir))
                    {
                        string[] dirs = Directory.GetDirectories(assetsDir);
                        foreach (string dir in dirs)
                        {
                            string name = Path.GetFileName(dir);
                            if (name.Length == 5 && ManagerServer.IsValidId(name))
                            {
                                list.Add(name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ManagerServer.Log("Error listing companies: " + ex.Message);
                }
                return Results.Ok(list);
            });

            // 8.1 List users for a company (excluding admin role)
            app.MapGet("/api/manager/companies/{companyId}/users", async (string companyId) =>
            {
                var validation = ManagerServer.ValidateCompanyId(companyId);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { error = validation.ErrorMessage });
                }

                string id = companyId.Trim().ToUpperInvariant();
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
                    ManagerServer.Log("Error listing users for company " + id + ": " + ex.Message);
                    return Results.StatusCode(500);
                }
            });

            // 8.2 Get permissions for a specific user in a company
            app.MapGet("/api/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId) =>
            {
                var validation = ManagerServer.ValidateCompanyId(companyId);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { error = validation.ErrorMessage });
                }

                string id = companyId.Trim().ToUpperInvariant();
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
                    ManagerServer.Log("Error loading permissions for user " + userId + " in company " + id + ": " + ex.Message);
                    return Results.StatusCode(500);
                }
            });

            // 8.3 Save permissions for a user in a company
            app.MapPost("/api/manager/companies/{companyId}/users/{userId:int}/permissions", async (string companyId, int userId, PermissionSettingsSpecification permissions) =>
            {
                var validation = ManagerServer.ValidateCompanyId(companyId);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { error = validation.ErrorMessage });
                }

                if (permissions == null)
                {
                    return Results.BadRequest(new { error = "Permissions body cannot be null." });
                }

                string id = companyId.Trim().ToUpperInvariant();
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
                        ManagerServer.Log($"Saved permissions successfully for user: {targetUser.Username} in company: {id}");
                        return Results.Ok(new { message = "Permissions updated successfully." });
                    }
                }
                catch (Exception ex)
                {
                    ManagerServer.Log("Error saving permissions for user " + userId + " in company " + id + ": " + ex.Message);
                    return Results.StatusCode(500);
                }
            });

            // 9. Load company configuration sync URLs
            app.MapGet("/api/companies/{id}", (string id) =>
            {
                var validation = ManagerServer.ValidateCompanyId(id);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { error = validation.ErrorMessage });
                }

                string companyId = id.Trim().ToUpperInvariant();
                string[] urls = ManagerServer.LoadCompanySyncUrls(companyId);
                return Results.Ok(new { companyId, urls });
            });

            // 10. Register or modify company config
            app.MapPost("/api/companies", (CompanySaveRequest req) =>
            {
                if (req == null) return Results.BadRequest(new { error = "Request body is empty." });

                var validation = ManagerServer.ValidateCompanyId(req.CompanyId);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { error = validation.ErrorMessage });
                }

                string companyId = req.CompanyId.Trim().ToUpperInvariant();
                string mode = req.Mode?.Trim() ?? "New";
                string syncUrlsText = req.SyncUrls ?? "";

                string rootDir = ManagerServer.GetRootDirectory();
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
                        ManagerServer.Log("Creating new tenant: " + companyId + " from template: BMS");
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
                            ManagerServer.Log("Warning: Template sync script not found: " + tmplSyncPath);
                        }

                        ManagerServer.WriteCompanyConfig(targetDir, companyId, syncUrlsText);
                        ManagerServer.Log("Success: Company " + companyId + " created and configuration saved!");
                        ManagerServer.TriggerDatabaseSync(companyId);

                        return Results.Ok(new { message = $"Company {companyId} created and registered successfully!" });
                    }
                    catch (Exception ex)
                    {
                        string errMsg = "Error creating company: " + ex.Message;
                        ManagerServer.Log(errMsg);
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
                        ManagerServer.Log("Modifying configuration for company: " + companyId);
                        ManagerServer.WriteCompanyConfig(targetDir, companyId, syncUrlsText);
                        ManagerServer.Log("Success: Company " + companyId + " configuration updated!");
                        ManagerServer.TriggerDatabaseSync(companyId);

                        return Results.Ok(new { message = $"Company {companyId} configuration updated successfully!" });
                    }
                    catch (Exception ex)
                    {
                        string errMsg = "Error modifying company: " + ex.Message;
                        ManagerServer.Log(errMsg);
                        return Results.StatusCode(500);
                    }
                }
            });

            // 11. Trigger sync explicitly
            app.MapPost("/api/companies/{id}/sync", (string id) =>
            {
                var validation = ManagerServer.ValidateCompanyId(id);
                if (!validation.IsValid)
                {
                    return Results.BadRequest(new { error = validation.ErrorMessage });
                }

                string companyId = id.Trim().ToUpperInvariant();
                ManagerServer.TriggerDatabaseSync(companyId);
                return Results.Ok(new { message = $"Database sync initiated for {companyId}." });
            });
        }
    }

    public class CompanySaveRequest
    {
        public string CompanyId { get; set; }
        public string Mode { get; set; }
        public string SyncUrls { get; set; }
    }
}
