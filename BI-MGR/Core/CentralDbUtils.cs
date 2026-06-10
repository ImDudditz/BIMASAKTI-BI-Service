using Bimasakti.BiService.Api.Controllers;
using Bimasakti.BiService.Api.Core;
using Bimasakti.BiService.Api.Models;
using System;
using System.IO;
using System.Linq;
using Bimasakti.BiService.Mgr.Models;
using Bimasakti.BiService.Api.Services;

namespace Bimasakti.BiService.Mgr.Core
{
    public static class CentralDbUtils
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> SchemaInitializedDbs = new();

        public static string GetCentralDbPath()
        {
            string dbPath = "";
            try
            {
                string configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                if (File.Exists(configPath))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
                    if (doc.RootElement.TryGetProperty("Config", out var config) && config.TryGetProperty("CentralDbPath", out var centralDbPath))
                    {
                        string pathFromConfig = centralDbPath.GetString();
                        if (!string.IsNullOrEmpty(pathFromConfig))
                        {
                            dbPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), pathFromConfig));
                            string dir = Path.GetDirectoryName(dbPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            {
                                Directory.CreateDirectory(dir);
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(dbPath))
            {
                string assetsDir = Bimasakti.BiService.Api.Core.DbUtils.GetAssetsDirectory();
                dbPath = Path.Combine(assetsDir, "BMS_BI_Central.db");
            }

            if (!SchemaInitializedDbs.ContainsKey(dbPath))
            {
                try
                {
                    using (var dbContext = new CentralDbContext(dbPath))
                    {
                        dbContext.Database.EnsureCreated();
                        
                        if (!dbContext.Companies.Any(c => c.CompanyId == "BMS"))
                        {
                            dbContext.Companies.Add(new Company { CompanyId = "BMS", IsActive = true, SyncConfigJson = "{}" });
                            dbContext.SaveChanges();
                            
                            try
                            {
                                string bmsDbPath = Bimasakti.BiService.Api.Core.DbUtils.GetSafeDbPath("BMS");
                                using var tenantDb = new CompanyDbContext(bmsDbPath);
                                tenantDb.Database.EnsureCreated();
                                
                                var widgetConfigService = new WidgetConfigService();
                                var allWidgets = widgetConfigService.GetAvailableWidgets().Select(w => w.Id).ToArray();
                                var allReports = new[] { "balance_sheet", "income_statement" };
                                var authSvc = new AuthenticationService();
                                
                                Action<string, string> ensureUser = (username, pass) =>
                                {
                                    var u = tenantDb.Users.FirstOrDefault(x => x.Username == username);
                                    if (u == null)
                                    {
                                        u = new User { Username = username, PasswordHash = authSvc.GetPasswordHash(pass), Role = "admin", CompanyId = "BMS", IsActive = true };
                                        tenantDb.Users.Add(u);
                                        tenantDb.SaveChanges();
                                        foreach (var w in allWidgets) tenantDb.UserWidgets.Add(new UserWidget { UserId = u.Id, WidgetKey = w, IsActive = true });
                                        foreach (var r in allReports) tenantDb.UserReports.Add(new UserReport { UserId = u.Id, ReportKey = r, IsActive = true });
                                    }
                                };

                                ensureUser("sa", "sfbmub");
                                ensureUser("realta", "nvctgc");
                                tenantDb.SaveChanges();
                            }
                            catch (Exception seedEx)
                            {
                                Console.WriteLine($"[DB] Warning: Failed to seed BMS tenant DB: {seedEx.Message}");
                            }
                        }
                    }
                    SchemaInitializedDbs.TryAdd(dbPath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DB] Warning: Failed to initialize schema for Central DB: {ex.Message}");
                }
            }
            return dbPath;
        }
    }
}

