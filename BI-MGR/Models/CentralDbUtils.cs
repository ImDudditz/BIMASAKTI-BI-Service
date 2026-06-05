using System;
using System.IO;

namespace Bimasakti.BiService.Mgr.Models
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
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(dbPath))
            {
                string assetsDir = Bimasakti.BiService.Api.Engines.svcDbUtils.GetAssetsDirectory();
                dbPath = Path.Combine(assetsDir, "BMS_BI_Central.db");
            }

            if (!SchemaInitializedDbs.ContainsKey(dbPath))
            {
                try
                {
                    using (var dbContext = new CentralDbContext(dbPath))
                    {
                        dbContext.Database.EnsureCreated();
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
