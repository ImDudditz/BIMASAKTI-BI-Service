using System;
using System.IO;

namespace BiPortal.FinancialReports.Manager.Models
{
    public static class CentralDbUtils
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> SchemaInitializedDbs = new();

        public static string GetCentralDbPath()
        {
            // Use BI-API's GetAssetsDirectory logic since it's already robust and shared
            string assetsDir = BMS_BI_SERVICE.Core.Engines.svcDbUtils.GetAssetsDirectory();
            string dbPath = Path.Combine(assetsDir, "BMS_BI_Central.db");
            
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
