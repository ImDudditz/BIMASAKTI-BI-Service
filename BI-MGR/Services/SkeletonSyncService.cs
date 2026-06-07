using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Bimasakti.BiService.Api.Core;
using Bimasakti.BiService.Mgr.Core;
using Bimasakti.BiService.Mgr.Models;
using Microsoft.EntityFrameworkCore;

namespace Bimasakti.BiService.Mgr.Services
{
    public class SkeletonSyncService
    {
        public async Task<(bool Success, string Message)> SyncBmsSkeletonsAsync()
        {
            try
            {
                string centralDbPath = CentralDbUtils.GetCentralDbPath();
                string bmsDbPath = DbUtils.GetSafeDbPath("BMS");
                
                var tablesToSync = new Dictionary<string, List<(string Name, string Type)>>();

                // 1. Gather all unique schemas from all active companies
                using (var centralDb = new CentralDbContext(centralDbPath))
                {
                    var activeCompanies = await centralDb.Companies
                        .Where(c => c.IsActive && c.CompanyId != "BMS")
                        .ToListAsync();

                    foreach (var company in activeCompanies)
                    {
                        string companyDbPath = DbUtils.GetSafeDbPath(company.CompanyId);
                        if (!File.Exists(companyDbPath)) continue;

                        try
                        {
                            using var conn = new SqliteConnection($"Data Source={companyDbPath};Mode=ReadOnly;");
                            await conn.OpenAsync();

                            var tableNames = new List<string>();
                            using (var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE 'Users' AND name NOT LIKE 'UserWidgets' AND name NOT LIKE 'UserReports';", conn))
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    tableNames.Add(reader.GetString(0));
                                }
                            }

                            foreach (var tableName in tableNames)
                            {
                                if (!tablesToSync.ContainsKey(tableName))
                                {
                                    var columns = new List<(string Name, string Type)>();
                                    using (var cmd = new SqliteCommand($"PRAGMA table_info({tableName});", conn))
                                    using (var reader = await cmd.ExecuteReaderAsync())
                                    {
                                        while (await reader.ReadAsync())
                                        {
                                            columns.Add((reader.GetString(1), reader.GetString(2)));
                                        }
                                    }
                                    tablesToSync[tableName] = columns;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SkeletonSync] Error reading from {company.CompanyId}: {ex.Message}");
                        }
                    }
                }

                if (tablesToSync.Count == 0)
                {
                    return (true, "No tables found across active companies.");
                }

                // 2. Sync to BMS Company DB
                using (var bmsConn = new SqliteConnection($"Data Source={bmsDbPath};"))
                {
                    await bmsConn.OpenAsync();
                    int tablesCreated = 0;

                    foreach (var kvp in tablesToSync)
                    {
                        string tableName = kvp.Key;
                        var columns = kvp.Value;

                        // Check if table exists in BMS
                        bool exists = false;
                        using (var cmd = new SqliteCommand("SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@name;", bmsConn))
                        {
                            cmd.Parameters.AddWithValue("@name", tableName);
                            exists = (long)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
                        }

                        if (!exists)
                        {
                            string colDefs = string.Join(", ", columns.Select(c => $"{c.Name} {c.Type}"));
                            using (var createCmd = new SqliteCommand($"CREATE TABLE {tableName} ({colDefs});", bmsConn))
                            {
                                await createCmd.ExecuteNonQueryAsync();
                            }

                            // Insert mock data
                            await InsertMockDataAsync(bmsConn, tableName, columns);
                            tablesCreated++;
                        }
                    }

                    return (true, $"Skeleton sync completed. {tablesCreated} new tables added to BMS Company DB.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkeletonSync] Critical Error: {ex}");
                return (false, $"Failed: {ex.Message}");
            }
        }

        private async Task InsertMockDataAsync(SqliteConnection conn, string tableName, List<(string Name, string Type)> columns)
        {
            var random = new Random();
            int rowsToInsert = 10;
            
            var colNames = string.Join(", ", columns.Select(c => c.Name));
            var paramNames = string.Join(", ", columns.Select(c => "@" + c.Name));
            
            string insertSql = $"INSERT INTO {tableName} ({colNames}) VALUES ({paramNames});";

            using var transaction = conn.BeginTransaction();
            try
            {
                using var cmd = new SqliteCommand(insertSql, conn, transaction);
                foreach (var col in columns)
                {
                    cmd.Parameters.Add(new SqliteParameter("@" + col.Name, DBNull.Value));
                }

                for (int i = 0; i < rowsToInsert; i++)
                {
                    foreach (var col in columns)
                    {
                        string type = (col.Type ?? "TEXT").ToUpperInvariant();
                        object mockValue = DBNull.Value;
                        string colName = col.Name.ToLowerInvariant();

                        if (type.Contains("INT"))
                        {
                            if (colName.Contains("id")) mockValue = random.Next(1000, 9999);
                            else if (colName.Contains("year")) mockValue = DateTime.Now.Year - random.Next(0, 5);
                            else if (colName.Contains("month") || colName.Contains("period")) mockValue = random.Next(1, 13);
                            else if (colName.Contains("status")) mockValue = random.Next(0, 3);
                            else mockValue = random.Next(1, 100);
                        }
                        else if (type.Contains("REAL") || type.Contains("DECIMAL") || type.Contains("NUMERIC"))
                        {
                            if (colName.Contains("amount") || colName.Contains("balance") || colName.Contains("budget"))
                                mockValue = Math.Round((random.NextDouble() * 10000000) + 1000, 2);
                            else if (colName.Contains("rate") || colName.Contains("percent"))
                                mockValue = Math.Round(random.NextDouble() * 100, 2);
                            else
                                mockValue = Math.Round(random.NextDouble() * 1000, 2);
                        }
                        else if (type.Contains("BOOL"))
                        {
                            mockValue = random.Next(2) == 0 ? 0 : 1;
                        }
                        else // TEXT, VARCHAR
                        {
                            if (colName.Contains("date") || colName.Contains("time"))
                                mockValue = DateTime.Now.AddDays(-random.Next(0, 365)).ToString("yyyy-MM-dd HH:mm:ss");
                            else if (colName.Contains("name") || colName.Contains("desc"))
                                mockValue = $"Mock {col.Name} {i + 1}";
                            else if (colName.Contains("account"))
                                mockValue = $"{random.Next(1000, 9999)} - Mock Account";
                            else if (colName.Contains("category"))
                                mockValue = $"Category {(char)('A' + random.Next(0, 5))}";
                            else
                                mockValue = $"Sample {col.Name} {i + 1}";
                        }

                        cmd.Parameters["@" + col.Name].Value = mockValue;
                    }
                    await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SkeletonSync] Error inserting mock data into {tableName}: {ex.Message}");
                await transaction.RollbackAsync();
            }
        }
    }
}
