using System;
using System.IO;
using System.Linq;

namespace BimasaktiReports.FinancialReports.Backend.Engines
{
    public static class svcDbUtils
    {
        // Equivalent to os.path.dirname(os.path.abspath(__file__))
        private static readonly string BaseDir = AppContext.BaseDirectory;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> CheckedPaths = new();

        public static string GetSafeDbPath(string companyId, string suffix = "")
        {
            // safe_id = "".join(c for c in company_id if c.isalnum() or c in ('-', '_')).upper()
            string safeId = new string(companyId
                .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
                .ToArray())
                .ToUpperInvariant();

            // Find backend directory robustly by walking up from BaseDir or Directory.GetCurrentDirectory()
            string? backendDir = null;
            string current = BaseDir;
            while (!string.IsNullOrEmpty(current))
            {
                string dirName = Path.GetFileName(current);
                if (dirName.Equals("backend", StringComparison.OrdinalIgnoreCase))
                {
                    backendDir = current;
                    break;
                }
                if (File.Exists(Path.Combine(current, "BimasaktiReports.slnx")))
                {
                    backendDir = Path.Combine(current, "backend");
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
                    if (dirName.Equals("backend", StringComparison.OrdinalIgnoreCase))
                    {
                        backendDir = current;
                        break;
                    }
                    if (File.Exists(Path.Combine(current, "BimasaktiReports.slnx")))
                    {
                        backendDir = Path.Combine(current, "backend");
                        break;
                    }
                    string? parent = Path.GetDirectoryName(current);
                    if (parent == current || string.IsNullOrEmpty(parent)) break;
                    current = parent;
                }
            }

            // Ultimate fallback to AppContext.BaseDirectory if backend directory is not found
            if (backendDir == null)
            {
                backendDir = BaseDir;
            }

            string dirPath = Path.GetFullPath(Path.Combine(backendDir, "assets", safeId));
            
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
                            bool shouldSwap = false;
                            if (!File.Exists(targetDb))
                            {
                                shouldSwap = true;
                            }
                            else
                            {
                                FileInfo targetInfo = new FileInfo(targetDb);
                                FileInfo duplicateInfo = new FileInfo(duplicateDb);
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
            return Path.Combine(dirPath, filename);
        }

        public static string GetGlrxTableName(string databasePath)
        {
            if (!File.Exists(databasePath)) return "GLRX0310";

            try
            {
                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                {
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

                using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                {
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
