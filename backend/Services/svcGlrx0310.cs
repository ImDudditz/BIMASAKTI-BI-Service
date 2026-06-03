using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace BiPortal.FinancialReports.Backend.Services
{
    public class LedgerReportItem
    {
        public decimal Balance { get; set; }
        public string No { get; set; } = "";
        public string Name { get; set; } = "";

        [System.Text.Json.Serialization.JsonPropertyName("end_budget")]
        public decimal EndBudget { get; set; }
    }

    public class LedgerReportGroup
    {
        public decimal Total { get; set; }
        public List<LedgerReportItem> Items { get; set; } = new();
    }

    public class LedgerReportSection
    {
        public decimal Total { get; set; }
        public Dictionary<string, LedgerReportGroup> Groups { get; set; } = new();
    }

    public class LedgerReportResponse
    {
        public string Status { get; set; } = "success";
        public Dictionary<string, LedgerReportSection> Data { get; set; } = new();
        public decimal NetIncome { get; set; }
        public decimal NetIncomeBudget { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IsvcGlrx0310
    {
        Task<LedgerReportResponse> GenerateLedgerReportAsync(
            string databasePath,
            string? year,
            string? period,
            string preset,
            string companyId);
    }

    public class svcGlrx0310 : IsvcGlrx0310
    {
        public async Task<LedgerReportResponse> GenerateLedgerReportAsync(
            string databasePath,
            string? year,
            string? period,
            string preset,
            string companyId)
        {
            var response = new LedgerReportResponse();
            response.Data["Assets"] = new LedgerReportSection();
            response.Data["Liabilities"] = new LedgerReportSection();
            response.Data["Equity"] = new LedgerReportSection();
            response.Data["Revenue"] = new LedgerReportSection();
            response.Data["Expenses"] = new LedgerReportSection();

            if (!System.IO.File.Exists(databasePath))
            {
                return new LedgerReportResponse
                {
                    Status = "error",
                    ErrorMessage = "Database file not found."
                };
            }

            try
            {
                var activeMappings = new Dictionary<string, JsonNode>();

                // --- Load Mappings directly from JSON Preset Files ---
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
                            foreach (var item in rawArray)
                            {
                                if (item.TryGetValue("account_no", out var accountNumber) && !string.IsNullOrEmpty(accountNumber))
                                {
                                    string section = item.TryGetValue("account_cat", out var categoryName) ? categoryName : "Expenses";
                                    string group = item.TryGetValue("group_name", out var groupName) ? groupName : "Uncategorized";
                                    
                                    var mappingNode = new JsonObject
                                    {
                                        ["section"] = section,
                                        ["group"] = group
                                    };
                                    activeMappings[accountNumber] = mappingNode;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback to empty mappings (default prefix mapping rules will apply)
                }

                using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
                await connection.OpenAsync();

                var schema = BiPortal.FinancialReports.Backend.Engines.svcDbUtils.GetGlrxSchema(databasePath);
                string tableName = schema.TableName;
                string yearCol = schema.YearColumn;
                string periodCol = schema.PeriodColumn;
                string endingBsisColumn = schema.EndBsisColumn;
                string endingBalanceColumn = schema.EndBalanceColumn;
                string endingBudgetColumn = schema.EndBudgetColumn;

                string ledgerQuery = $@"
                     SELECT 
                         account_no,
                         account_name,
                         SUM({endingBsisColumn}) AS ending_bsis,
                         SUM({endingBalanceColumn}) AS ending_balance,
                         SUM({endingBudgetColumn}) AS ending_budget
                     FROM {tableName}";

                var queryFilters = new List<string>();
                if (!string.IsNullOrEmpty(year))
                {
                    queryFilters.Add($"{yearCol} = @year");
                }
                if (!string.IsNullOrEmpty(period))
                {
                    queryFilters.Add($"{periodCol} = @period");
                }

                if (queryFilters.Count > 0)
                {
                    ledgerQuery += " WHERE " + string.Join(" AND ", queryFilters);
                }

                ledgerQuery += " GROUP BY account_no, account_name;";

                using var ledgerCommand = new SqliteCommand(ledgerQuery, connection);
                if (!string.IsNullOrEmpty(year))
                {
                    ledgerCommand.Parameters.AddWithValue("@year", year);
                }
                if (!string.IsNullOrEmpty(period))
                {
                    ledgerCommand.Parameters.AddWithValue("@period", period);
                }

                using var reader = await ledgerCommand.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string accountNumber = reader.IsDBNull(0) ? "" : reader.GetString(0).Trim();
                    if (string.IsNullOrEmpty(accountNumber))
                    {
                        continue;
                    }

                    string accountName = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
                    decimal endingBalanceSheetBsis = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    decimal endingBalance = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    decimal endingBudget = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);

                    char firstCharacter = accountNumber[0];
                    bool isCurrentEarningAccount = accountNumber.Contains("current earning", StringComparison.OrdinalIgnoreCase) ||
                                                    accountName.Contains("current earning", StringComparison.OrdinalIgnoreCase) ||
                                                    accountName.Contains("Laba Periode Berjalan", StringComparison.OrdinalIgnoreCase) ||
                                                    accountName.Contains("Laba ditahan tahun Berjalan", StringComparison.OrdinalIgnoreCase);

                    // Layout section mappings
                    string targetSectionName;
                    string targetGroupName;

                    if (isCurrentEarningAccount)
                    {
                        targetSectionName = "Equity";
                        targetGroupName = "Laba/Rugi Tahun Lalu";
                    }
                    else
                    {
                        activeMappings.TryGetValue(accountNumber, out var mappedRuleElement);
                        targetGroupName = mappedRuleElement?["group"]?.GetValue<string>() ?? "Uncategorized";
                        string? mappedSectionName = mappedRuleElement?["section"]?.GetValue<string>();

                        if (!string.IsNullOrEmpty(mappedSectionName))
                        {
                            targetSectionName = mappedSectionName;
                        }
                        else
                        {
                            if (firstCharacter == '1')
                            {
                                targetSectionName = "Assets";
                            }
                            else if (firstCharacter == '2')
                            {
                                targetSectionName = "Liabilities";
                            }
                            else if (firstCharacter == '3')
                            {
                                targetSectionName = "Equity";
                            }
                            else if (firstCharacter == '4')
                            {
                                targetSectionName = "Revenue";
                            }
                            else
                            {
                                targetSectionName = "Expenses";
                            }
                        }
                    }

                    // Balance calculation inverting logic
                    decimal balance;
                    if (isCurrentEarningAccount)
                    {
                        balance = endingBalance;
                    }
                    else if (targetSectionName == "Equity")
                    {
                        balance = endingBalance; // Use end_balance as Y axis for Equity
                    }
                    else if (firstCharacter == '3')
                    {
                        balance = endingBalance * -1; // Use end_balance as Y axis for Equity
                    }
                    else if (firstCharacter == '1' || firstCharacter == '5' || firstCharacter == '6' || firstCharacter == '7' || firstCharacter == '8' || firstCharacter == '9')
                    {
                        balance = endingBalanceSheetBsis;
                    }
                    else if (firstCharacter == '2' || firstCharacter == '4')
                    {
                        balance = endingBalanceSheetBsis * -1;
                    }
                    else
                    {
                        balance = endingBalanceSheetBsis;
                    }

                    if (!response.Data.TryGetValue(targetSectionName, out var section))
                    {
                        section = new LedgerReportSection();
                        response.Data[targetSectionName] = section;
                    }

                    if (!section.Groups.TryGetValue(targetGroupName, out var group))
                    {
                        group = new LedgerReportGroup();
                        section.Groups[targetGroupName] = group;
                    }

                    section.Total += balance;
                    group.Total += balance;
                    group.Items.Add(new LedgerReportItem
                    {
                        Balance = balance,
                        No = accountNumber,
                        Name = accountName,
                        EndBudget = endingBudget
                    });
                }

                decimal revenueTotal = response.Data.TryGetValue("Revenue", out var retrievedRevenueSection) ? retrievedRevenueSection.Total : 0;
                decimal expensesTotal = response.Data.TryGetValue("Expenses", out var retrievedExpensesSection) ? retrievedExpensesSection.Total : 0;
                response.NetIncome = revenueTotal - expensesTotal;

                decimal revenueBudgetTotal = 0;
                if (response.Data.TryGetValue("Revenue", out var revenueSection))
                {
                    foreach (var group in revenueSection.Groups.Values)
                    {
                        foreach (var item in group.Items)
                        {
                            revenueBudgetTotal += item.EndBudget;
                        }
                    }
                }

                decimal expensesBudgetTotal = 0;
                if (response.Data.TryGetValue("Expenses", out var expensesSection))
                {
                    foreach (var group in expensesSection.Groups.Values)
                    {
                        foreach (var item in group.Items)
                        {
                            expensesBudgetTotal += item.EndBudget;
                        }
                    }
                }
                response.NetIncomeBudget = revenueBudgetTotal - expensesBudgetTotal;

                return response;
            }
            catch (Exception exception)
            {
                return new LedgerReportResponse
                {
                    Status = "error",
                    ErrorMessage = exception.Message
                };
            }
        }
    }
}
