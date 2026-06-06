using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Models;
using Microsoft.Data.Sqlite;

namespace Bimasakti.BiService.Api.Services
{
    public interface IDynamicDataService
    {
        Task<List<Dictionary<string, object>>> ExecuteWidgetQueryAsync(string databasePath, DsbiQueryConfig queryConfig);
    }

    public class DynamicDataService : IDynamicDataService
    {
        public async Task<List<Dictionary<string, object>>> ExecuteWidgetQueryAsync(string databasePath, DsbiQueryConfig queryConfig)
        {
            var resultList = new List<Dictionary<string, object>>();

            if (!System.IO.File.Exists(databasePath))
                return resultList;

            if (string.IsNullOrWhiteSpace(queryConfig.Table))
                return resultList;

            try
            {
                using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
                await connection.OpenAsync();

                var selectParts = new List<string>();
                var groupByParts = new List<string>();
                
                // Add Dimensions
                foreach (var dim in queryConfig.Dimensions)
                {
                    if (!string.IsNullOrWhiteSpace(dim))
                    {
                        selectParts.Add(dim);
                        groupByParts.Add(dim);
                    }
                }

                // Add Measures
                foreach (var measure in queryConfig.Measures)
                {
                    if (!string.IsNullOrWhiteSpace(measure.Field) && !string.IsNullOrWhiteSpace(measure.Agg))
                    {
                        // Safely handle typical aggregations
                        var agg = measure.Agg.ToUpper();
                        if (agg != "SUM" && agg != "COUNT" && agg != "AVG" && agg != "MIN" && agg != "MAX") agg = "SUM";
                        selectParts.Add($"{agg}({measure.Field}) AS {agg}_{measure.Field}");
                    }
                }

                if (!selectParts.Any())
                {
                    selectParts.Add("*");
                }

                var sql = $"SELECT {string.Join(", ", selectParts)} FROM {queryConfig.Table}";

                // Add Filters
                if (queryConfig.Filters.Any())
                {
                    var filterParts = new List<string>();
                    for(int i = 0; i < queryConfig.Filters.Count; i++)
                    {
                        var filter = queryConfig.Filters[i];
                        filterParts.Add($"{filter.Field} {filter.Operator} @val{i}");
                    }
                    sql += " WHERE " + string.Join(" AND ", filterParts);
                }

                if (groupByParts.Any())
                {
                    sql += " GROUP BY " + string.Join(", ", groupByParts);
                }

                using var command = new SqliteCommand(sql, connection);
                
                if (queryConfig.Filters.Any())
                {
                    for(int i = 0; i < queryConfig.Filters.Count; i++)
                    {
                        command.Parameters.AddWithValue($"@val{i}", queryConfig.Filters[i].Value);
                    }
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    resultList.Add(row);
                }
            }
            catch (Exception ex)
            {
                // In production, log the exception.
                Console.WriteLine($"Error executing dynamic query: {ex.Message}");
            }

            return resultList;
        }
    }
}
