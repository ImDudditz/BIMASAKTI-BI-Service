using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Models;
using Bimasakti.BiService.Api.Core;
using Microsoft.Data.Sqlite;

namespace Bimasakti.BiService.Api.Services
{
    public interface IDynamicDataService
    {
        Task<List<Dictionary<string, object?>>> ExecuteWidgetQueryAsync(string databasePath, DsbiQueryConfig queryConfig, string? year = null, string? period = null);
    }

    public class DynamicDataService : IDynamicDataService
    {
        public async Task<List<Dictionary<string, object?>>> ExecuteWidgetQueryAsync(string databasePath, DsbiQueryConfig queryConfig, string? year = null, string? period = null)
        {
            var resultList = new List<Dictionary<string, object?>>();

            if (!System.IO.File.Exists(databasePath))
                return resultList;

            if (string.IsNullOrWhiteSpace(queryConfig.Table))
                return resultList;

            // Handle custom KPI cards query
            if (queryConfig.Table.Equals("GLRX0310_KPI", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteKpiCardsQueryAsync(databasePath, year, period);
            }

            try
            {
                using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
                await connection.OpenAsync();

                var schema = DbUtils.GetGlrxSchema(databasePath);
                string actualTable = queryConfig.Table;
                bool isLedgerTable = queryConfig.Table.StartsWith("GLRX03", StringComparison.OrdinalIgnoreCase);

                if (isLedgerTable)
                {
                    actualTable = schema.TableName;
                }

                var selectParts = new List<string>();
                var groupByParts = new List<string>();
                
                // Add Dimensions
                foreach (var dim in queryConfig.Dimensions)
                {
                    if (!string.IsNullOrWhiteSpace(dim))
                    {
                        string mappedDim = dim;
                        if (isLedgerTable)
                        {
                            if (dim.Equals("period", StringComparison.OrdinalIgnoreCase)) mappedDim = schema.PeriodColumn;
                            else if (dim.Equals("year", StringComparison.OrdinalIgnoreCase)) mappedDim = schema.YearColumn;
                        }
                        else if (queryConfig.Table.Equals("CBRX7000", StringComparison.OrdinalIgnoreCase))
                        {
                            if (dim.Equals("period", StringComparison.OrdinalIgnoreCase)) mappedDim = "Period";
                        }
                        selectParts.Add($"{mappedDim} AS {dim}");
                        groupByParts.Add(mappedDim);
                    }
                }

                // Add Measures
                foreach (var measure in queryConfig.Measures)
                {
                    if (!string.IsNullOrWhiteSpace(measure.Field) && !string.IsNullOrWhiteSpace(measure.Agg))
                    {
                        var agg = measure.Agg.ToUpper();
                        if (agg != "SUM" && agg != "COUNT" && agg != "AVG" && agg != "MIN" && agg != "MAX") agg = "SUM";
                        
                        string mappedField = measure.Field;
                        if (isLedgerTable)
                        {
                            if (measure.Field.Equals("end_balance", StringComparison.OrdinalIgnoreCase)) mappedField = schema.EndBalanceColumn;
                            else if (measure.Field.Equals("end_budget", StringComparison.OrdinalIgnoreCase)) mappedField = schema.EndBudgetColumn;
                            else if (measure.Field.Equals("ptd_budget", StringComparison.OrdinalIgnoreCase)) mappedField = schema.PtdBudgetColumn;
                        }
                        else if (queryConfig.Table.Equals("CBRX7000", StringComparison.OrdinalIgnoreCase))
                        {
                            if (measure.Field.Equals("ptd_amount", StringComparison.OrdinalIgnoreCase)) mappedField = "ABS(actual_amount)";
                        }
                        selectParts.Add($"{agg}({mappedField}) AS {agg}_{measure.Field}");
                    }
                }

                if (!selectParts.Any())
                {
                    selectParts.Add("*");
                }

                var sql = $"SELECT {string.Join(", ", selectParts)} FROM {actualTable}";

                var filterParts = new List<string>();
                var parameters = new List<SqliteParameter>();

                // Add original Filters
                if (queryConfig.Filters != null && queryConfig.Filters.Any())
                {
                    for(int i = 0; i < queryConfig.Filters.Count; i++)
                    {
                        var filter = queryConfig.Filters[i];
                        string mappedField = filter.Field;
                        if (isLedgerTable)
                        {
                            if (filter.Field.Equals("period", StringComparison.OrdinalIgnoreCase)) mappedField = schema.PeriodColumn;
                            else if (filter.Field.Equals("year", StringComparison.OrdinalIgnoreCase)) mappedField = schema.YearColumn;
                            else if (filter.Field.Equals("end_balance", StringComparison.OrdinalIgnoreCase)) mappedField = schema.EndBalanceColumn;
                            else if (filter.Field.Equals("end_budget", StringComparison.OrdinalIgnoreCase)) mappedField = schema.EndBudgetColumn;
                            else if (filter.Field.Equals("ptd_budget", StringComparison.OrdinalIgnoreCase)) mappedField = schema.PtdBudgetColumn;
                        }
                        else if (queryConfig.Table.Equals("CBRX7000", StringComparison.OrdinalIgnoreCase))
                        {
                            if (filter.Field.Equals("period", StringComparison.OrdinalIgnoreCase)) mappedField = "Period";
                            else if (filter.Field.Equals("ptd_amount", StringComparison.OrdinalIgnoreCase)) mappedField = "actual_amount";
                        }
                        filterParts.Add($"{mappedField} {filter.Operator} @val{i}");
                        parameters.Add(new SqliteParameter($"@val{i}", filter.Value));
                    }
                }

                // Inject dynamic year filter if table is ledger table
                if (isLedgerTable)
                {
                    if (!string.IsNullOrEmpty(year))
                    {
                        filterParts.Add($"{schema.YearColumn} = @dyn_year");
                        parameters.Add(new SqliteParameter("@dyn_year", year));
                    }
                }
                else if (queryConfig.Table.Equals("CBRX7000", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(year))
                    {
                        filterParts.Add("year = @dyn_year");
                        parameters.Add(new SqliteParameter("@dyn_year", year));
                    }
                }

                if (filterParts.Any())
                {
                    sql += " WHERE " + string.Join(" AND ", filterParts);
                }

                if (groupByParts.Any())
                {
                    sql += " GROUP BY " + string.Join(", ", groupByParts);
                }

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddRange(parameters.ToArray());

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    resultList.Add(row);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing dynamic query: {ex.Message}");
            }

            return resultList;
        }

        private async Task<List<Dictionary<string, object?>>> ExecuteKpiCardsQueryAsync(string databasePath, string? year, string? period)
        {
            var results = new List<Dictionary<string, object?>>();
            try
            {
                using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
                await connection.OpenAsync();

                var schema = DbUtils.GetGlrxSchema(databasePath);
                string tableName = schema.TableName;
                string yearCol = schema.YearColumn;
                string periodCol = schema.PeriodColumn;
                string endBsisCol = schema.EndBsisColumn;

                // 1. Get max year/period if not provided
                if (string.IsNullOrEmpty(year) || string.IsNullOrEmpty(period))
                {
                    string maxQuery = $"SELECT MAX({yearCol}), MAX({periodCol}) FROM {tableName};";
                    using var maxCmd = new SqliteCommand(maxQuery, connection);
                    using var maxReader = await maxCmd.ExecuteReaderAsync();
                    if (await maxReader.ReadAsync())
                    {
                        if (string.IsNullOrEmpty(year)) year = maxReader.IsDBNull(0) ? "2026" : maxReader.GetString(0);
                        if (string.IsNullOrEmpty(period)) period = maxReader.IsDBNull(1) ? "12" : maxReader.GetString(1);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(year)) year = "2026";
                        if (string.IsNullOrEmpty(period)) period = "12";
                    }
                }

                // Calculate last year
                string lastYear = year;
                if (int.TryParse(year, out int y))
                {
                    lastYear = (y - 1).ToString();
                }

                // 2. Query Revenue and Expenditure for selected year/period and last year/period
                var dataQuery = $@"
                    SELECT 
                        {yearCol} AS year,
                        SUM(CASE WHEN bs_is = 'Income Statement' AND account_no LIKE '4%' THEN {endBsisCol} * -1 ELSE 0 END) AS revenue_ytd,
                        SUM(CASE WHEN bs_is = 'Income Statement' AND account_no NOT LIKE '4%' THEN {endBsisCol} ELSE 0 END) AS expenditure_ytd,
                        SUM(CASE WHEN bs_is = 'Income Statement' AND account_no LIKE '4%' THEN {schema.PtdBsisColumn} * -1 ELSE 0 END) AS revenue_current,
                        SUM(CASE WHEN bs_is = 'Income Statement' AND account_no NOT LIKE '4%' THEN {schema.PtdBsisColumn} ELSE 0 END) AS expenditure_current
                    FROM {tableName}
                    WHERE {yearCol} IN (@year, @lastYear) AND {periodCol} = @period
                    GROUP BY {yearCol};";

                using var cmd = new SqliteCommand(dataQuery, connection);
                cmd.Parameters.AddWithValue("@year", year);
                cmd.Parameters.AddWithValue("@lastYear", lastYear);
                cmd.Parameters.AddWithValue("@period", period);

                decimal currentRevenueYtd = 0;
                decimal currentExpenditureYtd = 0;
                decimal currentRevenueCurrent = 0;
                decimal currentExpenditureCurrent = 0;

                decimal lastRevenueYtd = 0;
                decimal lastExpenditureYtd = 0;
                decimal lastRevenueCurrent = 0;
                decimal lastExpenditureCurrent = 0;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string rYear = reader.GetString(0);
                    decimal revYtd = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    decimal expYtd = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    decimal revCurr = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    decimal expCurr = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);

                    if (rYear == year)
                    {
                        currentRevenueYtd = revYtd;
                        currentExpenditureYtd = expYtd;
                        currentRevenueCurrent = revCurr;
                        currentExpenditureCurrent = expCurr;
                    }
                    else if (rYear == lastYear)
                    {
                        lastRevenueYtd = revYtd;
                        lastExpenditureYtd = expYtd;
                        lastRevenueCurrent = revCurr;
                        lastExpenditureCurrent = expCurr;
                    }
                }

                decimal currentNetYtd = currentRevenueYtd - currentExpenditureYtd;
                decimal lastNetYtd = lastRevenueYtd - lastExpenditureYtd;
                decimal growthYtd = 0;

                if (lastNetYtd != 0)
                {
                    growthYtd = ((currentNetYtd - lastNetYtd) / Math.Abs(lastNetYtd)) * 100;
                }
                else if (currentNetYtd != 0)
                {
                    growthYtd = 100;
                }

                decimal currentNetCurrent = currentRevenueCurrent - currentExpenditureCurrent;
                decimal lastNetCurrent = lastRevenueCurrent - lastExpenditureCurrent;
                decimal growthCurrent = 0;

                if (lastNetCurrent != 0)
                {
                    growthCurrent = ((currentNetCurrent - lastNetCurrent) / Math.Abs(lastNetCurrent)) * 100;
                }
                else if (currentNetCurrent != 0)
                {
                    growthCurrent = 100;
                }

                results.Add(new Dictionary<string, object?> {
                    ["id"] = "revenue",
                    ["title"] = "Revenue",
                    ["ytd_value"] = (double)currentRevenueYtd,
                    ["current_value"] = (double)currentRevenueCurrent,
                    ["icon"] = "trending-up",
                    ["format"] = "currency"
                });
                results.Add(new Dictionary<string, object?> {
                    ["id"] = "expenditure",
                    ["title"] = "Expenditure",
                    ["ytd_value"] = (double)currentExpenditureYtd,
                    ["current_value"] = (double)currentExpenditureCurrent,
                    ["icon"] = "trending-down",
                    ["format"] = "currency"
                });
                results.Add(new Dictionary<string, object?> {
                    ["id"] = "net_income",
                    ["title"] = "Income / Loss",
                    ["ytd_value"] = (double)currentNetYtd,
                    ["current_value"] = (double)currentNetCurrent,
                    ["icon"] = "dollar-sign",
                    ["format"] = "currency"
                });
                results.Add(new Dictionary<string, object?> {
                    ["id"] = "growth",
                    ["title"] = "Net Income Growth",
                    ["ytd_value"] = (double)Math.Round(growthYtd, 2),
                    ["current_value"] = (double)Math.Round(growthCurrent, 2),
                    ["icon"] = "percent",
                    ["format"] = "percentage"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running KPI cards query: {ex.Message}");
            }
            return results;
        }
    }
}
