using Bimasakti.BiService.Api.Controllers;
using Bimasakti.BiService.Api.Core;
using Bimasakti.BiService.Api.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Bimasakti.BiService.Api.Core
{
#pragma warning disable IDE1006 // Naming Styles
    public static class svcDbUtils
    {
        // Equivalent to os.path.dirname(os.path.abspath(__file__))
        private static readonly string BaseDir = AppContext.BaseDirectory;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> CheckedPaths = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> SchemaInitializedDbs = new();

        public static void EnsureSchemaInitialized(string databasePath)
        {
            if (SchemaInitializedDbs.ContainsKey(databasePath)) return;

            try
            {
                using (var dbContext = new CompanyDbContext(databasePath))
                {
                    dbContext.Database.EnsureCreated();
                }
                SchemaInitializedDbs.TryAdd(databasePath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] Warning: Failed to initialize schema for {databasePath}: {ex.Message}");
            }
        }

        public static string GetAssetsDirectory()
        {
            try
            {
                string configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                if (File.Exists(configPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                    if (doc.RootElement.TryGetProperty("Config", out var config) && config.TryGetProperty("ReferenceDir", out var refDir))
                    {
                        string dir = refDir.GetString() ?? "";
                        if (!string.IsNullOrEmpty(dir))
                        {
                            string fullDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), dir));
                            if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);
                            return fullDir;
                        }
                    }
                }
            }
            catch { }

            string? backendDir = null;
            string current = BaseDir;
            while (!string.IsNullOrEmpty(current))
            {
                string dirName = Path.GetFileName(current);
                if (dirName.Equals("BI-API", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("BI-API", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("BI-API-Standalone", StringComparison.OrdinalIgnoreCase))
                {
                    backendDir = current;
                    break;
                }
                if (File.Exists(Path.Combine(current, "BI_SERVICE.slnx")))
                {
                    backendDir = Path.Combine(current, "BI-API");
                    break;
                }
                // Support production Publish directories where Manager and Core are side-by-side
                if (Directory.Exists(Path.Combine(current, "BI-API")))
                {
                    backendDir = Path.Combine(current, "BI-API");
                    break;
                }
                if (Directory.Exists(Path.Combine(current, "BI-API-Standalone")))
                {
                    backendDir = Path.Combine(current, "BI-API-Standalone");
                    break;
                }

                string? parent = Path.GetDirectoryName(current);
                if (parent == current || string.IsNullOrEmpty(parent)) break;
                current = parent;
            }

            if (backendDir == null)
            {
                current = Directory.GetCurrentDirectory();
                while (!string.IsNullOrEmpty(current))
                {
                    string dirName = Path.GetFileName(current);
                    if (dirName.Equals("BI-API", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("BI-API", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("BI-API-Standalone", StringComparison.OrdinalIgnoreCase))
                    {
                        backendDir = current;
                        break;
                    }
                    if (File.Exists(Path.Combine(current, "BI_SERVICE.slnx")))
                    {
                        backendDir = Path.Combine(current, "BI-API");
                        break;
                    }
                    // Support production Publish directories
                    if (Directory.Exists(Path.Combine(current, "BI-API")))
                    {
                        backendDir = Path.Combine(current, "BI-API");
                        break;
                    }
                    if (Directory.Exists(Path.Combine(current, "BI-API-Standalone")))
                    {
                        backendDir = Path.Combine(current, "BI-API-Standalone");
                        break;
                    }

                    string? parent = Path.GetDirectoryName(current);
                    if (parent == current || string.IsNullOrEmpty(parent)) break;
                    current = parent;
                }
            }

            backendDir ??= BaseDir;

            string assetsDir = Path.GetFullPath(Path.Combine(backendDir, "assets"));
            if (!Directory.Exists(assetsDir))
            {
                Directory.CreateDirectory(assetsDir);
            }
            return assetsDir;
        }



        public static string GetSafeDbPath(string companyId, string suffix = "")
        {
            // safe_id = "".join(c for c in company_id if c.isalnum() or c in ('-', '_')).upper()
            string safeId = new string([.. companyId
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')])
                .ToUpperInvariant();

            string assetsDir = GetAssetsDirectory();
            string companyDir = Path.Combine(assetsDir, "Company");
            if (!Directory.Exists(companyDir))
            {
                Directory.CreateDirectory(companyDir);
            }
            string dirPath = Path.GetFullPath(Path.Combine(companyDir, safeId));

            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            // Auto-recovery / Self-healing for duplicate files (e.g. ASHMD (1).db)
            if (string.IsNullOrEmpty(suffix))
            {
                string targetDb = Path.Combine(dirPath, $"{safeId}.db");
                string duplicateDb = Path.Combine(dirPath, $"{safeId} (1).db");

                if (!CheckedPaths.ContainsKey(targetDb))
                {
                    if (File.Exists(duplicateDb))
                    {
                        try
                        {
                            // Clear connection pool to release file handles before delete/move operations
                            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                            bool shouldSwap = false;
                            if (!File.Exists(targetDb))
                            {
                                shouldSwap = true;
                            }
                            else
                            {
                                FileInfo targetInfo = new(targetDb);
                                FileInfo duplicateInfo = new(duplicateDb);
                                // If target is less than 1MB (newly initialized) and duplicate is larger
                                if (targetInfo.Length < 1024 * 1024 && duplicateInfo.Length > targetInfo.Length)
                                {
                                    shouldSwap = true;
                                    File.Delete(targetDb);
                                }
                            }

                            if (shouldSwap)
                            {
                                File.Move(duplicateDb, targetDb);
                            }
                            else
                            {
                                // Clean up the conflict duplicate file
                                File.Delete(duplicateDb);
                            }
                        }
                        catch
                        {
                            // Safe fallback on file lock/IO exceptions
                        }
                    }
                    CheckedPaths.TryAdd(targetDb, true);
                }
            }

            string filename = $"{safeId}{suffix}.db";
            string dbPath = Path.Combine(dirPath, filename);

            if (string.IsNullOrEmpty(suffix))
            {
                EnsureSchemaInitialized(dbPath);
            }

            return dbPath;
        }

        public static string GetGlrxTableName(string databasePath)
        {
            if (!File.Exists(databasePath)) return "GLRX0310";

            try
            {
                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
                connection.Open();

                // Check if GLRX0310 exists
                using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name='GLRX0310';", connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value) return "GLRX0310";
                }

                // Otherwise search for any table matching GLRX03%
                using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'GLRX03%' ORDER BY name DESC LIMIT 1;", connection))
                {
                    var result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        string tName = result.ToString()!;
                        if (tName.Length > 0 && char.IsLetter(tName[0]) && tName.All(c => char.IsLetterOrDigit(c) || c == '_'))
                        {
                            return tName;
                        }
                    }
                }
            }
            catch
            {
                // Fallback on error
            }

            return "GLRX0310";
        }

        public static GlrxSchema GetGlrxSchema(string databasePath)
        {
            var schema = new GlrxSchema();
            if (!File.Exists(databasePath)) return schema;

            try
            {
                string tableName = GetGlrxTableName(databasePath);
                schema.TableName = tableName;

                using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
                connection.Open();
                var columns = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new Microsoft.Data.Sqlite.SqliteCommand($"PRAGMA table_info({tableName});", connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string colName = reader.GetString(1);
                        columns.Add(colName);
                    }
                }

                // Map Year Column
                if (columns.Contains("year")) schema.YearColumn = "year";
                else if (columns.Contains("year_period")) schema.YearColumn = "year_period";

                // Map Period Column
                if (columns.Contains("period")) schema.PeriodColumn = "period";
                else if (columns.Contains("month_period")) schema.PeriodColumn = "month_period";

                // Map BegBalance Column
                if (columns.Contains("beg_balance")) schema.BegBalanceColumn = "beg_balance";
                else if (columns.Contains("beginning_balance")) schema.BegBalanceColumn = "beginning_balance";

                // Map EndBalance Column
                if (columns.Contains("end_balance")) schema.EndBalanceColumn = "end_balance";
                else if (columns.Contains("ending_balance")) schema.EndBalanceColumn = "ending_balance";

                // Map BegBudget Column
                if (columns.Contains("beg_budget")) schema.BegBudgetColumn = "beg_budget";
                else if (columns.Contains("beginning_budget")) schema.BegBudgetColumn = "beginning_budget";

                // Map PtdBudget Column
                if (columns.Contains("ptd_budget")) schema.PtdBudgetColumn = "ptd_budget";

                // Map EndBudget Column
                if (columns.Contains("end_budget")) schema.EndBudgetColumn = "end_budget";
                else if (columns.Contains("ending_budget")) schema.EndBudgetColumn = "ending_budget";

                // Map BegBsis Column
                if (columns.Contains("beg_bsis")) schema.BegBsisColumn = "beg_bsis";
                else if (columns.Contains("beginning_bsis")) schema.BegBsisColumn = "beginning_bsis";

                // Map PtdBsis Column
                if (columns.Contains("ptd_bsis")) schema.PtdBsisColumn = "ptd_bsis";

                // Map EndBsis Column
                if (columns.Contains("end_bsis")) schema.EndBsisColumn = "end_bsis";
                else if (columns.Contains("ending_bsis")) schema.EndBsisColumn = "ending_bsis";
            }
            catch
            {
                // Fallback on error
            }

            return schema;
        }
    }

    public class GlrxSchema
    {
        public string TableName { get; set; } = "GLRX0310";
        public string YearColumn { get; set; } = "year";
        public string PeriodColumn { get; set; } = "period";
        public string BegBalanceColumn { get; set; } = "beg_balance";
        public string EndBalanceColumn { get; set; } = "end_balance";
        public string BegBudgetColumn { get; set; } = "beg_budget";
        public string PtdBudgetColumn { get; set; } = "ptd_budget";
        public string EndBudgetColumn { get; set; } = "end_budget";
        public string BegBsisColumn { get; set; } = "beg_bsis";
        public string PtdBsisColumn { get; set; } = "ptd_bsis";
        public string EndBsisColumn { get; set; } = "end_bsis";
    }
}
