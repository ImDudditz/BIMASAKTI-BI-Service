using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BimasaktiReports.FinancialReports.Backend.Services;
using Microsoft.AspNetCore.Authorization;

namespace BimasaktiReports.FinancialReports.Backend.Engines.Reports
{
    // --- PAYLOAD DEFINITIONS ---
    public class MappingSpecification
    {
        [JsonPropertyName("company_id")]
        public string CompanyId { get; set; } = "";
        public string Preset { get; set; } = "";
        public JsonNode Data { get; set; } = new JsonObject();
    }

    [Authorize]
    [ApiController]
    [Route("api")]
    public class rpGlrx0310Controller : ControllerBase
    {
        private readonly IsvcGLRX0310 _ledgerService;
        private readonly IsvcDatabaseSyncService _syncService;

        public rpGlrx0310Controller(IsvcGLRX0310 ledgerService, IsvcDatabaseSyncService syncService)
        {
            _ledgerService = ledgerService;
            _syncService = syncService;
        }

        // --- 1. Available Filters ---
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            if (!System.IO.File.Exists(databasePath))
            {
                return Ok(new { status = "error", message = "Database not found." });
            }

            try
            {
                var yearsList = new List<string>();
                var periodsList = new List<string>();
                string companyNameString = "";
                string latestYear = "";
                string latestPeriod = "";

                using (var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                {
                    await connection.OpenAsync();

                    var schema = svcDbUtils.GetGlrxSchema(databasePath);
                    string tableName = schema.TableName;
                    string yearCol = schema.YearColumn;
                    string periodCol = schema.PeriodColumn;

                    // Query distinct years
                    string yearsQuery = $"SELECT DISTINCT {yearCol} FROM {tableName} WHERE {yearCol} IS NOT NULL AND {yearCol} != '';";
                    using (var command = new SqliteCommand(yearsQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            yearsList.Add(reader.GetString(0));
                        }
                    }

                    // Query distinct periods
                    string periodsQuery = $"SELECT DISTINCT {periodCol} FROM {tableName} WHERE {periodCol} IS NOT NULL AND {periodCol} != '';";
                    using (var command = new SqliteCommand(periodsQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            periodsList.Add(reader.GetString(0));
                        }
                    }

                    // Query latest year and period with transactions
                    string latestQuery = $"SELECT {yearCol}, {periodCol} FROM {tableName} WHERE {yearCol} IS NOT NULL AND {yearCol} != '' AND {periodCol} IS NOT NULL AND {periodCol} != '' ORDER BY {yearCol} DESC, {periodCol} DESC LIMIT 1;";
                    using (var command = new SqliteCommand(latestQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            latestYear = reader.GetString(0);
                            latestPeriod = reader.GetString(1);
                        }
                    }

                    // Query company name description
                    string nameQuery = $"SELECT company_name FROM {tableName} WHERE company_id = @cid LIMIT 1;";
                    using (var command = new SqliteCommand(nameQuery, connection))
                    {
                        command.Parameters.AddWithValue("@cid", companyId.ToUpperInvariant());
                        var resultValue = await command.ExecuteScalarAsync();
                        if (resultValue != null && resultValue != DBNull.Value)
                        {
                            companyNameString = resultValue.ToString() ?? "";
                        }
                    }
                }

                return Ok(new
                {
                    status = "success",
                    years = yearsList.OrderByDescending(y => y).ToList(),
                    periods = periodsList.OrderBy(p => p).ToList(),
                    latest_year = latestYear,
                    latest_period = latestPeriod,
                    company_id = companyId,
                    company_name = companyNameString
                });
            }
            catch (Exception)
            {
                return Ok(new { status = "error", message = "An unexpected system error occurred." });
            }
        }

        // --- 2. Chart of Accounts (COA) ---
        [HttpGet("reports/coa")]
        public async Task<IActionResult> GetChartOfAccounts()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);
            var chartOfAccountsList = new List<object>();

            if (!System.IO.File.Exists(databasePath))
            {
                return Ok(chartOfAccountsList);
            }

            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                {
                    await connection.OpenAsync();
                    string tableName = svcDbUtils.GetGlrxTableName(databasePath);
                    string coaQuery = $"SELECT DISTINCT account_no, account_name FROM {tableName} WHERE account_no IS NOT NULL AND account_no != '' ORDER BY account_no;";
                    using (var command = new SqliteCommand(coaQuery, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            chartOfAccountsList.Add(new
                            {
                                account_no = reader.GetString(0).Trim(),
                                account_name = reader.GetString(1).Trim()
                            });
                        }
                    }
                }
            }
            catch
            {
                // Graceful empty fallback on failure
            }

            return Ok(chartOfAccountsList);
        }

        // --- 3. COA Mappings presets ---
        [HttpGet("mappings")]
        public async Task<IActionResult> GetMappings([FromQuery] string preset)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);
            
            try
            {
                string configDirectory = Path.GetDirectoryName(databasePath)!;
                string normalizedPreset = preset;
                if (preset.StartsWith("preset", StringComparison.OrdinalIgnoreCase) && preset.Length > 6)
                {
                    normalizedPreset = "Preset" + preset.Substring(6);
                }
                string presetJsonPath = Path.Combine(configDirectory, $"{companyId.ToUpperInvariant()}_{normalizedPreset}.json");

                if (System.IO.File.Exists(presetJsonPath))
                {
                    string jsonContent = await System.IO.File.ReadAllTextAsync(presetJsonPath);
                    var rawArray = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonContent);
                    if (rawArray != null)
                    {
                        var mappingsDict = new Dictionary<string, object>();
                        foreach (var item in rawArray)
                        {
                            if (item.TryGetValue("account_no", out var accNo) && !string.IsNullOrEmpty(accNo))
                            {
                                string section = item.TryGetValue("account_cat", out var cat) ? (cat ?? "Expenses") : "Expenses";
                                string group = item.TryGetValue("group_name", out var grp) ? (grp ?? "Uncategorized") : "Uncategorized";
                                mappingsDict[accNo] = new { section, group };
                            }
                        }
                        return Ok(mappingsDict);
                    }
                }
            }
            catch
            {
                // Graceful fallback
            }

            return Ok(new JsonObject());
        }

        [HttpPost("mappings")]
        public async Task<IActionResult> SaveMappings([FromBody] MappingSpecification mappingRequest)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();
            mappingRequest.CompanyId = companyId; // Force override with trusted claim

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                string configDirectory = Path.GetDirectoryName(databasePath)!;
                string preset = mappingRequest.Preset;
                
                // Security: Prevent writing to arbitrary config files (DoS / Path Traversal)
                if (preset == null || (!preset.Equals("preset1", StringComparison.OrdinalIgnoreCase) && 
                                       !preset.Equals("preset2", StringComparison.OrdinalIgnoreCase) && 
                                       !preset.Equals("preset3", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { status = "error", message = "Invalid preset specified. Must be preset1, preset2, or preset3." });
                }
                
                string normalizedPreset = preset;
                if (preset.StartsWith("preset", StringComparison.OrdinalIgnoreCase) && preset.Length > 6)
                {
                    normalizedPreset = "Preset" + preset.Substring(6);
                }
                string presetJsonPath = Path.Combine(configDirectory, $"{companyId.ToUpperInvariant()}_{normalizedPreset}.json");

                var incomingDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(mappingRequest.Data.ToJsonString()) 
                                   ?? new Dictionary<string, Dictionary<string, string>>();

                // Try to load existing mappings to preserve any mapping details that might not be in the incoming request (fallback database sync)
                var existingPresetDict = new Dictionary<string, Dictionary<string, string>>();
                if (System.IO.File.Exists(presetJsonPath))
                {
                    try
                    {
                        string existingContent = await System.IO.File.ReadAllTextAsync(presetJsonPath);
                        var existingList = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(existingContent);
                        if (existingList != null)
                        {
                            foreach (var item in existingList)
                            {
                                if (item.TryGetValue("account_no", out var accNo) && !string.IsNullOrEmpty(accNo))
                                {
                                    existingPresetDict[accNo.Trim()] = item;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore corruption
                    }
                }

                // Retrieve master chart of accounts from database
                var dbAccounts = new List<(string AccountNo, string AccountName)>();
                if (System.IO.File.Exists(databasePath))
                {
                    using (var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                    {
                        await connection.OpenAsync();
                        string tableName = svcDbUtils.GetGlrxTableName(databasePath);
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

                List<Dictionary<string, string>> accountsList = new List<Dictionary<string, string>>();

                if (dbAccounts.Count > 0)
                {
                    // Rebuild accountsList strictly using active database accounts, preventing stale template accounts
                    foreach (var dbAcc in dbAccounts)
                    {
                        string accNo = dbAcc.AccountNo;
                        string accName = dbAcc.AccountName;

                        string category = "Expenses";
                        if (accNo.StartsWith("1")) category = "Assets";
                        else if (accNo.StartsWith("2")) category = "Liabilities";
                        else if (accNo.StartsWith("3")) category = "Equity";
                        else if (accNo.StartsWith("4")) category = "Revenue";

                        string groupName = "Uncategorized";

                        // 1. Try to get from incoming mapping payload
                        if (incomingDict.TryGetValue(accNo, out var ruleDict))
                        {
                            if (ruleDict.TryGetValue("section", out var section))
                            {
                                category = section;
                            }
                            if (ruleDict.TryGetValue("group", out var group))
                            {
                                groupName = group;
                            }
                        }
                        // 2. Try to fall back to existing preset file mapping if not in incoming dict
                        else if (existingPresetDict.TryGetValue(accNo, out var existingRow))
                        {
                            if (existingRow.TryGetValue("account_cat", out var existingCat))
                            {
                                category = existingCat;
                            }
                            if (existingRow.TryGetValue("group_name", out var existingGroup))
                            {
                                groupName = existingGroup;
                            }
                        }

                        accountsList.Add(new Dictionary<string, string>
                        {
                            ["company_id"] = mappingRequest.CompanyId,
                            ["account_no"] = accNo,
                            ["account_name"] = accName,
                            ["account_cat"] = category,
                            ["group_name"] = groupName
                        });
                    }

                    // Add "Current Earning" fallback if it exists in the preset or as a constant
                    string ceKey = "Current Earning";
                    if (incomingDict.TryGetValue(ceKey, out var ceRule))
                    {
                        accountsList.Add(new Dictionary<string, string>
                        {
                            ["company_id"] = mappingRequest.CompanyId,
                            ["account_no"] = ceKey,
                            ["account_name"] = "Current Earning Last Year",
                            ["account_cat"] = ceRule.TryGetValue("section", out var ceSec) ? ceSec : "Expenses",
                            ["group_name"] = ceRule.TryGetValue("group", out var ceGrp) ? ceGrp : "Uncategorized"
                        });
                    }
                    else if (existingPresetDict.TryGetValue(ceKey, out var ceRow))
                    {
                        accountsList.Add(new Dictionary<string, string>
                        {
                            ["company_id"] = mappingRequest.CompanyId,
                            ["account_no"] = ceKey,
                            ["account_name"] = "Current Earning Last Year",
                            ["account_cat"] = ceRow.TryGetValue("account_cat", out var ceCat) ? ceCat : "Expenses",
                            ["group_name"] = ceRow.TryGetValue("group_name", out var ceGrp) ? ceGrp : "Uncategorized"
                        });
                    }
                    else
                    {
                        accountsList.Add(new Dictionary<string, string>
                        {
                            ["company_id"] = mappingRequest.CompanyId,
                            ["account_no"] = ceKey,
                            ["account_name"] = "Current Earning Last Year",
                            ["account_cat"] = "Expenses",
                            ["group_name"] = "Uncategorized"
                        });
                    }
                }
                else
                {
                    // Fallback to legacy behavior if database is not available
                    if (System.IO.File.Exists(presetJsonPath))
                    {
                        try
                        {
                            string existingContent = await System.IO.File.ReadAllTextAsync(presetJsonPath);
                            accountsList = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(existingContent) ?? new List<Dictionary<string, string>>();
                        }
                        catch
                        {
                            accountsList = new List<Dictionary<string, string>>();
                        }
                    }
                    else
                    {
                        accountsList = new List<Dictionary<string, string>>();
                    }

                    foreach (var row in accountsList)
                    {
                        if (row.TryGetValue("account_no", out var accNo) && incomingDict.TryGetValue(accNo, out var ruleDict))
                        {
                            if (ruleDict.TryGetValue("section", out var section))
                            {
                                row["account_cat"] = section;
                            }
                            if (ruleDict.TryGetValue("group", out var group))
                            {
                                row["group_name"] = group;
                            }
                        }
                    }
                }

                var serializeOptions = new JsonSerializerOptions { WriteIndented = true };
                string jsonOutput = JsonSerializer.Serialize(accountsList, serializeOptions);
                await System.IO.File.WriteAllTextAsync(presetJsonPath, jsonOutput);

                return Ok(new { status = "success" });
            }
            catch (Exception exception)
            {
                return StatusCode(500, new { status = "error", message = exception.Message });
            }
        }

        [HttpDelete("mappings")]
        public async Task<IActionResult> DeleteMapping([FromQuery] string preset)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                string configDirectory = Path.GetDirectoryName(databasePath)!;
                
                // Security: Prevent arbitrary config deletion
                if (preset == null || (!preset.Equals("preset1", StringComparison.OrdinalIgnoreCase) && 
                                       !preset.Equals("preset2", StringComparison.OrdinalIgnoreCase) && 
                                       !preset.Equals("preset3", StringComparison.OrdinalIgnoreCase)))
                {
                    return BadRequest(new { status = "error", message = "Invalid preset specified." });
                }

                string normalizedPreset = preset;
                if (preset.StartsWith("preset", StringComparison.OrdinalIgnoreCase) && preset.Length > 6)
                {
                    normalizedPreset = "Preset" + preset.Substring(6);
                }
                string presetJsonPath = Path.Combine(configDirectory, $"{companyId.ToUpperInvariant()}_{normalizedPreset}.json");

                // Query database to reconstruct clean, database-aligned mapping list
                var dbAccounts = new List<(string AccountNo, string AccountName)>();
                if (System.IO.File.Exists(databasePath))
                {
                    using (var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                    {
                        await connection.OpenAsync();
                        string tableName = svcDbUtils.GetGlrxTableName(databasePath);
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

                List<Dictionary<string, string>> accountsList = new List<Dictionary<string, string>>();
                if (dbAccounts.Count > 0)
                {
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
                            ["company_id"] = companyId,
                            ["account_no"] = accNo,
                            ["account_name"] = accName,
                            ["account_cat"] = category,
                            ["group_name"] = "Uncategorized"
                        });
                    }

                    accountsList.Add(new Dictionary<string, string>
                    {
                        ["company_id"] = companyId,
                        ["account_no"] = "Current Earning",
                        ["account_name"] = "Current Earning Last Year",
                        ["account_cat"] = "Expenses",
                        ["group_name"] = "Uncategorized"
                    });
                }
                else
                {
                    // Fallback to legacy behavior if database is not available
                    if (System.IO.File.Exists(presetJsonPath))
                    {
                        try
                        {
                            string existingContent = await System.IO.File.ReadAllTextAsync(presetJsonPath);
                            accountsList = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(existingContent) ?? new List<Dictionary<string, string>>();
                        }
                        catch
                        {
                            accountsList = new List<Dictionary<string, string>>();
                        }

                        foreach (var row in accountsList)
                        {
                            if (row.TryGetValue("account_no", out var accNo))
                            {
                                string category = "Expenses";
                                if (accNo.StartsWith("1")) category = "Assets";
                                else if (accNo.StartsWith("2")) category = "Liabilities";
                                else if (accNo.StartsWith("3")) category = "Equity";
                                else if (accNo.StartsWith("4")) category = "Revenue";

                                row["account_cat"] = category;
                                row["group_name"] = "Uncategorized";
                            }
                        }
                    }
                }

                var serializeOptions = new JsonSerializerOptions { WriteIndented = true };
                string jsonOutput = JsonSerializer.Serialize(accountsList, serializeOptions);
                await System.IO.File.WriteAllTextAsync(presetJsonPath, jsonOutput);

                return Ok(new { status = "success" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = "error", message = "An unexpected system error occurred." });
            }
        }

        // --- 4. COA Preset Names ---
        [HttpGet("preset-names")]
        public async Task<IActionResult> GetPresetNames()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            var defaultPresetNames = new Dictionary<string, string>
            {
                { "preset1", "Preset 1" },
                { "preset2", "Preset 2" },
                { "preset3", "Preset 3" }
            };

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);
            if (!System.IO.File.Exists(databasePath))
            {
                return Ok(defaultPresetNames);
            }

            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;"))
                {
                    await connection.OpenAsync();
                    string namesQuery = "SELECT names_data FROM coa_preset_names WHERE company_id = @cid;";
                    using (var command = new SqliteCommand(namesQuery, connection))
                    {
                        command.Parameters.AddWithValue("@cid", companyId.ToUpperInvariant());
                        var resultValue = await command.ExecuteScalarAsync();
                        if (resultValue != null && resultValue != DBNull.Value)
                        {
                            return Ok(JsonSerializer.Deserialize<Dictionary<string, string>>(resultValue.ToString() ?? "{}"));
                        }
                    }
                }
            }
            catch
            {
                // Fallback
            }

            return Ok(defaultPresetNames);
        }

        [HttpPost("preset-names")]
        public async Task<IActionResult> SavePresetNames([FromBody] MappingSpecification mappingRequest)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();
            mappingRequest.CompanyId = companyId;

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            try
            {
                using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                {
                    await connection.OpenAsync();

                    string tableCreateQuery = @"
                        CREATE TABLE IF NOT EXISTS coa_preset_names (
                            company_id TEXT NOT NULL PRIMARY KEY, 
                            names_data JSON
                        );";
                    using (var command = new SqliteCommand(tableCreateQuery, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    string insertQuery = @"
                        INSERT INTO coa_preset_names (company_id, names_data) 
                        VALUES (@cid, @data)
                        ON CONFLICT(company_id) DO UPDATE SET names_data=excluded.names_data;";
                    using (var command = new SqliteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@cid", mappingRequest.CompanyId.ToUpperInvariant());
                        command.Parameters.AddWithValue("@data", mappingRequest.Data.ToJsonString());

                        await command.ExecuteNonQueryAsync();
                    }
                }

                return Ok(new { status = "success" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { status = "error", message = "An unexpected system error occurred." });
            }
        }

        // --- 5. Ledger Report Generator ---
        [HttpGet("reports/ledger")]
        public async Task<IActionResult> GetLedgerReport(
            [FromQuery] string? year = null,
            [FromQuery] string? period = null,
            [FromQuery] string preset = "preset1")
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            string databasePath = svcDbUtils.GetSafeDbPath(companyId);

            var responseResult = await _ledgerService.GenerateLedgerReportAsync(databasePath, year, period, preset, companyId);
            
            if (responseResult.Status == "error")
            {
                return Ok(new { status = "error", message = responseResult.ErrorMessage });
            }

            return Ok(new
            {
                status = "success",
                data = responseResult.Data,
                net_income = responseResult.NetIncome,
                net_income_budget = responseResult.NetIncomeBudget
            });
        }

        // --- 6. Database Synchronization ---
        [HttpPost("sync")]
        public async Task<IActionResult> SyncDatabase()
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim)) return Unauthorized(new { detail = "Invalid token claims" });
            string companyId = companyIdClaim.ToUpperInvariant();

            var result = await _syncService.SyncCompanyDatabaseAsync(companyId);
            if (!result.Success)
            {
                return Ok(new { status = "error", message = result.Message });
            }
            return Ok(new { status = "success", message = result.Message });
        }
    }
}
