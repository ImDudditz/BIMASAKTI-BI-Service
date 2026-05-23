using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BimasaktiReports.FinancialReports.Backend.Services;

namespace BimasaktiReports.FinancialReports.Backend.Engines.Dashboard
{
    [ApiController]
    [Route("api")]
    public class dsGlrx0310 : ControllerBase
    {
        private readonly IsvcDashboardAnalyticsService _dashboardAnalyticsService;
        private static readonly HttpClient HttpClientInstance = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        public dsGlrx0310(IsvcDashboardAnalyticsService dashboardAnalyticsService)
        {
            _dashboardAnalyticsService = dashboardAnalyticsService;
        }

        // --- 1. Dynamic Azure Data Sync ---
        [HttpPost("dev-load-data")]
        public async Task<IActionResult> LoadDataOnce([FromQuery(Name = "company_id")] string companyId = "ASHMD")
        {
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);
            string configDirectory = Path.GetDirectoryName(databasePath)!;
            string configFilePath = Path.Combine(configDirectory, $"{companyId}_config.json");

            try
            {
                if (!System.IO.File.Exists(configFilePath))
                {
                    return NotFound(new { status = "error", message = $"Config file {companyId}_config.json not found" });
                }

                string jsonConfigContent = await System.IO.File.ReadAllTextAsync(configFilePath);
                var configDocument = JsonSerializer.Deserialize<JsonNode>(jsonConfigContent);
                var syncUrlsMap = configDocument?["sync_urls"]?.AsObject();

                if (syncUrlsMap == null || syncUrlsMap.Count == 0)
                {
                    return BadRequest(new { status = "error", message = $"Key 'sync_urls' missing or empty in {companyId}_config.json" });
                }

                var syncResults = new Dictionary<string, int>();

                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    await connection.OpenAsync();

                    foreach (var syncUrlElement in syncUrlsMap)
                    {
                        string tableName = syncUrlElement.Key;
                        if (string.IsNullOrEmpty(tableName) || !char.IsLetter(tableName[0]) || !tableName.All(c => char.IsLetterOrDigit(c) || c == '_'))
                        {
                            return BadRequest(new { status = "error", message = $"Invalid table name configured: {tableName}" });
                        }
                        string? remoteUrl = syncUrlElement.Value?.GetValue<string>();

                        if (string.IsNullOrEmpty(remoteUrl))
                        {
                            syncResults[tableName] = 0;
                            continue;
                        }

                        var httpResponse = await HttpClientInstance.GetAsync(remoteUrl);
                        httpResponse.EnsureSuccessStatusCode();
                        string datasetJsonText = await httpResponse.Content.ReadAsStringAsync();

                        var datasetElements = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(datasetJsonText);
                        if (datasetElements == null || datasetElements.Count == 0)
                        {
                            syncResults[tableName] = 0;
                            continue;
                        }

                        // Replicate autoload_with dynamic SQLite column mapping
                        var targetTableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        using (var schemaCommand = new SqliteCommand($"PRAGMA table_info({tableName});", connection))
                        {
                            using (var schemaReader = await schemaCommand.ExecuteReaderAsync())
                            {
                                while (await schemaReader.ReadAsync())
                                {
                                    targetTableColumns.Add(schemaReader.GetString(1));
                                }
                            }
                        }

                        var insertPayloadList = new List<Dictionary<string, string>>();
                        foreach (var dataRow in datasetElements)
                        {
                            var rowData = new Dictionary<string, string>();
                            foreach (var keyValuePair in dataRow)
                            {
                                if (targetTableColumns.Contains(keyValuePair.Key))
                                {
                                    rowData[keyValuePair.Key] = keyValuePair.Value.ValueKind switch
                                    {
                                        JsonValueKind.Null => "",
                                        JsonValueKind.String => keyValuePair.Value.GetString() ?? "",
                                        _ => keyValuePair.Value.GetRawText().Trim('"')
                                    };
                                }
                            }

                            if (targetTableColumns.Contains("company_id"))
                            {
                                rowData["company_id"] = companyId;
                            }

                            insertPayloadList.Add(rowData);
                        }

                        using (var dbTransaction = connection.BeginTransaction())
                        {
                            // Clear existing rows
                            using (var deleteCommand = new SqliteCommand($"DELETE FROM {tableName};", connection, dbTransaction))
                            {
                                await deleteCommand.ExecuteNonQueryAsync();
                            }

                            // Perform batch inserts
                            if (insertPayloadList.Count > 0)
                            {
                                foreach (var insertRow in insertPayloadList)
                                {
                                    var parameterKeysString = string.Join(", ", insertRow.Keys);
                                    var parameterValuesString = string.Join(", ", insertRow.Keys.Select(k => $"@{k}"));
                                    string insertQueryString = $"INSERT INTO {tableName} ({parameterKeysString}) VALUES ({parameterValuesString});";

                                    using (var insertCommand = new SqliteCommand(insertQueryString, connection, dbTransaction))
                                    {
                                        foreach (var keyValuePair in insertRow)
                                        {
                                            insertCommand.Parameters.AddWithValue($"@{keyValuePair.Key}", keyValuePair.Value);
                                        }
                                        await insertCommand.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                            await dbTransaction.CommitAsync();
                        }

                        syncResults[tableName] = insertPayloadList.Count;
                    }
                }

                return Ok(new
                {
                    status = "success",
                    rows_loaded = syncResults.Values.Sum(),
                    details = syncResults
                });
            }
            catch (Exception exception)
            {
                return Ok(new { status = "error", message = exception.Message });
            }
        }

        // --- 2. Property Operations Desk Metrics ---
        [HttpGet("v1/dashboard/operation/metrics")]
        public async Task<IActionResult> GetOperationsMetrics([FromQuery(Name = "company_id")] string companyId = "ASHMD")
        {
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            var analyticsResult = await _dashboardAnalyticsService.GetOperationsMetricsAsync(databasePath, companyId);
            return Ok(analyticsResult);
        }

        // --- 3. Property Maintenance Desk Status ---
        [HttpGet("v1/dashboard/maintenance/status")]
        public async Task<IActionResult> GetMaintenanceStatus([FromQuery(Name = "company_id")] string companyId = "ASHMD")
        {
            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            var analyticsResult = await _dashboardAnalyticsService.GetMaintenanceStatusAsync(databasePath, companyId);
            return Ok(analyticsResult);
        }
    }
}
