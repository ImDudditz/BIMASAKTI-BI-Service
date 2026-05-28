using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using BimasaktiReports.FinancialReports.Backend.Engines;

namespace BimasaktiReports.FinancialReports.Backend.Services
{
    public interface IsvcDatabaseSyncService
    {
        Task<(bool Success, string Message)> SyncCompanyDatabaseAsync(string companyId);
    }

    public class svcDatabaseSyncService : IsvcDatabaseSyncService
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public async Task<(bool Success, string Message)> SyncCompanyDatabaseAsync(string companyId)
        {
            string dbPath = svcDbUtils.GetSafeDbPath(companyId);
            string companyDir = Path.GetDirectoryName(dbPath) ?? "";
            string configPath = Path.Combine(companyDir, $"{companyId.ToUpperInvariant()}_config.json");
            string logPath = Path.Combine(companyDir, $"{companyId.ToUpperInvariant()}_history.log");

            if (!File.Exists(configPath))
            {
                return (false, $"Configuration file not found: {configPath}");
            }

            var logLines = new List<string>();
            Action<string, string> logMessage = (level, msg) =>
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {level} - {msg}";
                logLines.Add(line);
            };

            logMessage("INFO", $"=== Starting Sync Job for Tenant: {companyId} ===");

            try
            {
                string configContent = await File.ReadAllTextAsync(configPath);
                using var configDoc = JsonDocument.Parse(configContent);
                if (!configDoc.RootElement.TryGetProperty("sync_urls", out var syncUrlsProp) || syncUrlsProp.ValueKind != JsonValueKind.Object)
                {
                    logMessage("WARNING", "No 'sync_urls' found in config file. Nothing to sync.");
                    await AppendLogFileAsync(logPath, logLines);
                    return (false, "No 'sync_urls' section found in config.");
                }

                using var connection = new SqliteConnection($"Data Source={dbPath}");
                await connection.OpenAsync();

                int successTables = 0;
                int totalRowsSynced = 0;

                foreach (var property in syncUrlsProp.EnumerateObject())
                {
                    string tableName = property.Name;
                    if (string.IsNullOrEmpty(tableName) || !char.IsLetter(tableName[0]) || !tableName.All(c => char.IsLetterOrDigit(c) || c == '_'))
                    {
                        logMessage("ERROR", $"Invalid table name: '{tableName}'. skipping sync.");
                        continue;
                    }
                    string url = property.Value.GetString() ?? "";

                    if (string.IsNullOrEmpty(url)) continue;

                    // SSRF Protection: Validate URI scheme and prevent loopback/local requests
                    if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUri))
                    {
                        logMessage("ERROR", $"Invalid URL format: {url}");
                        continue;
                    }
                    if (parsedUri.Scheme != Uri.UriSchemeHttps)
                    {
                        logMessage("ERROR", $"Insecure URL scheme rejected. Only HTTPS is allowed: {url}");
                        continue;
                    }
                    
                    bool isPrivateOrLocal = parsedUri.IsLoopback || parsedUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || parsedUri.Host.Equals("169.254.169.254");
                    if (parsedUri.HostNameType == UriHostNameType.IPv4 && System.Net.IPAddress.TryParse(parsedUri.Host, out var ipAddress))
                    {
                        var ipBytes = ipAddress.GetAddressBytes();
                        if (ipBytes[0] == 10 || 
                            (ipBytes[0] == 172 && ipBytes[1] >= 16 && ipBytes[1] <= 31) || 
                            (ipBytes[0] == 192 && ipBytes[1] == 168))
                        {
                            isPrivateOrLocal = true;
                        }
                    }

                    if (isPrivateOrLocal)
                    {
                        logMessage("ERROR", $"Blocked request to internal/local address: {parsedUri.Host}");
                        continue;
                    }

                    logMessage("INFO", $"Processing table: {tableName} from URL: {url}");

                    try
                    {
                        using var response = await HttpClient.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        string jsonContent = await response.Content.ReadAsStringAsync();

                        using var datasetDoc = JsonDocument.Parse(jsonContent);
                        if (datasetDoc.RootElement.ValueKind != JsonValueKind.Array || datasetDoc.RootElement.GetArrayLength() == 0)
                        {
                            logMessage("WARNING", $"Skipping {tableName}: Payload is empty or not an array.");
                            continue;
                        }

                        var dataset = datasetDoc.RootElement;
                        var firstRecord = dataset[0];

                        // Build CREATE TABLE column list
                        var createCols = new List<string>();
                        var colNames = new List<string>();
                        var placeholders = new List<string>();

                        foreach (var prop in firstRecord.EnumerateObject())
                        {
                            string colName = prop.Name;
                            if (string.IsNullOrEmpty(colName) || !char.IsLetter(colName[0]) || !colName.All(c => char.IsLetterOrDigit(c) || c == '_'))
                            {
                                logMessage("ERROR", $"Invalid column name in table '{tableName}': '{colName}'. Skipping this table sync.");
                                throw new InvalidOperationException($"Invalid column name: {colName}");
                            }
                            colNames.Add(colName);
                            placeholders.Add($"@{colName}");

                            switch (prop.Value.ValueKind)
                            {
                                case JsonValueKind.Number:
                                    if (prop.Value.TryGetInt64(out _))
                                        createCols.Add($"{colName} INTEGER");
                                    else
                                        createCols.Add($"{colName} REAL");
                                    break;
                                default:
                                    createCols.Add($"{colName} TEXT");
                                    break;
                            }
                        }

                        using var transaction = connection.BeginTransaction();
                        try
                        {
                            // Create Table
                            string createQuery = $"CREATE TABLE IF NOT EXISTS {tableName} ({string.Join(", ", createCols)});";
                            using (var cmd = new SqliteCommand(createQuery, connection, transaction))
                            {
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Clear Table
                            string deleteQuery = $"DELETE FROM {tableName};";
                            using (var cmd = new SqliteCommand(deleteQuery, connection, transaction))
                            {
                                await cmd.ExecuteNonQueryAsync();
                            }

                            // Insert Data
                            string insertQuery = $"INSERT INTO {tableName} ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", placeholders)});";

                            foreach (var record in dataset.EnumerateArray())
                            {
                                using var insertCmd = new SqliteCommand(insertQuery, connection, transaction);
                                foreach (var colName in colNames)
                                {
                                    if (record.TryGetProperty(colName, out var val))
                                    {
                                        switch (val.ValueKind)
                                        {
                                            case JsonValueKind.Null:
                                                insertCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                                                break;
                                            case JsonValueKind.Number:
                                                if (val.TryGetDecimal(out var decVal))
                                                    insertCmd.Parameters.AddWithValue($"@{colName}", decVal);
                                                else
                                                    insertCmd.Parameters.AddWithValue($"@{colName}", val.GetDouble());
                                                break;
                                            case JsonValueKind.True:
                                                insertCmd.Parameters.AddWithValue($"@{colName}", 1);
                                                break;
                                            case JsonValueKind.False:
                                                insertCmd.Parameters.AddWithValue($"@{colName}", 0);
                                                break;
                                            default:
                                                insertCmd.Parameters.AddWithValue($"@{colName}", val.GetString() ?? "");
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        insertCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                                    }
                                }
                                await insertCmd.ExecuteNonQueryAsync();
                            }

                            await transaction.CommitAsync();
                            int rowCount = dataset.GetArrayLength();
                            logMessage("INFO", $"Successfully replaced {rowCount} rows in {tableName}.");
                            successTables++;
                            totalRowsSynced += rowCount;
                        }
                        catch (Exception txEx)
                        {
                            await transaction.RollbackAsync();
                            logMessage("ERROR", $"Transaction rollback. Database error while replacing {tableName}: {txEx.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logMessage("ERROR", $"Failed to fetch or process data for {tableName}: {ex.Message}");
                    }
                }

                logMessage("INFO", $"=== Sync Job Completed for Tenant: {companyId} ===");
                await AppendLogFileAsync(logPath, logLines);
                await InitializeDefaultPresets(companyId, dbPath);
                return (true, $"Synchronized {successTables} tables with {totalRowsSynced} total rows successfully.");
            }
            catch (Exception ex)
            {
                logMessage("CRITICAL", $"Critical error in sync job: {ex.Message}");
                await AppendLogFileAsync(logPath, logLines);
                return (false, $"Critical sync failure: {ex.Message}");
            }
        }

        private async Task InitializeDefaultPresets(string companyId, string dbPath)
        {
            try
            {
                string companyDir = Path.GetDirectoryName(dbPath) ?? "";
                string[] presets = new[] { "Preset1", "Preset2", "Preset3" };

                // Get master accounts from DB
                var dbAccounts = new List<(string AccountNo, string AccountName)>();
                if (File.Exists(dbPath))
                {
                    using (var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;"))
                    {
                        await connection.OpenAsync();
                        string tableName = svcDbUtils.GetGlrxTableName(dbPath);
                        string query = $"SELECT DISTINCT account_no, account_name FROM {tableName} WHERE account_no IS NOT NULL AND account_no != '' ORDER BY account_no;";
                        using (var command = new SqliteCommand(query, connection))
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                dbAccounts.Add((reader.GetString(0).Trim(), reader.GetString(1).Trim()));
                            }
                        }
                    }
                }

                if (dbAccounts.Count == 0) return;

                foreach (string preset in presets)
                {
                    string presetJsonPath = Path.Combine(companyDir, $"{companyId.ToUpperInvariant()}_{preset}.json");
                    if (!File.Exists(presetJsonPath))
                    {
                        var accountsList = new List<Dictionary<string, string>>();
                        foreach (var dbAcc in dbAccounts)
                        {
                            string accNo = dbAcc.AccountNo;
                            string accName = dbAcc.AccountName;

                            string category = "Expenses";
                            if (accNo.StartsWith("1")) category = "Assets";
                            else if (accNo.StartsWith("2")) category = "Liabilities";
                            else if (accNo.StartsWith("3")) category = "Equity";
                            else if (accNo.StartsWith("4")) category = "Revenue";

                            accountsList.Add(new Dictionary<string, string>
                            {
                                ["company_id"] = companyId.ToUpperInvariant(),
                                ["account_no"] = accNo,
                                ["account_name"] = accName,
                                ["account_cat"] = category,
                                ["group_name"] = "Uncategorized"
                            });
                        }

                        // Add "Current Earning" fallback
                        accountsList.Add(new Dictionary<string, string>
                        {
                            ["company_id"] = companyId.ToUpperInvariant(),
                            ["account_no"] = "Current Earning",
                            ["account_name"] = "Current Earning Last Year",
                            ["account_cat"] = "Expenses",
                            ["group_name"] = "Uncategorized"
                        });

                        var serializeOptions = new JsonSerializerOptions { WriteIndented = true };
                        string jsonOutput = JsonSerializer.Serialize(accountsList, serializeOptions);
                        await File.WriteAllTextAsync(presetJsonPath, jsonOutput);
                    }
                }
            }
            catch
            {
                // Non-blocking: preset initialization is a best-effort background task.
                // Failures here do not affect the main sync result.
            }
        }

        private async Task AppendLogFileAsync(string logPath, List<string> lines)
        {
            try
            {
                await File.AppendAllLinesAsync(logPath, lines);
            }
            catch
            {
                // Ignore log write errors
            }
        }
    }
}
