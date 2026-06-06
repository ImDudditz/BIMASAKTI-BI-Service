using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Core;

namespace Bimasakti.BiService.Api.Services.Engines
{
    public class TicketSummaryRecord
    {
        public string Category { get; set; } = "Unknown";
        public string CallStatus { get; set; } = "Unknown";
        public int TicketCount { get; set; }
    }

    public class Lmrx0800KpiResponse
    {
        public int TotalTickets { get; set; }
        public int ComplaintTickets { get; set; }
        public int MaintenanceTickets { get; set; }
        public int RequestTickets { get; set; }
        public int ClosedTickets { get; set; }
    }

    public class SemanticModelResponse
    {
        public string CompanyId { get; set; } = "";
        public string Domain { get; set; } = "Lease Management";
        public string Dataset { get; set; } = "tickets_summary";
        public List<string> Dimensions { get; set; } = new List<string> { "category", "call_status" };
        public List<string> Measures { get; set; } = new List<string> { "ticket_count" };
        public List<TicketSummaryRecord> Data { get; set; } = new List<TicketSummaryRecord>();
    }

    public interface IsvcLmrx0800
    {
        Task<object> GetDataAsync(string companyId);
        Task<SemanticModelResponse?> GetTicketsSummaryAsync(string companyId);
        Task<Lmrx0800KpiResponse?> GetTicketKpisAsync(string companyId, string year, string period);
    }

    public class svcLmrx0800 : IsvcLmrx0800
    {
        public async Task<object> GetDataAsync(string companyId)
        {
            string databasePath = DbUtils.GetSafeDbPath(companyId);
            if (!System.IO.File.Exists(databasePath)) return new List<object>();

            var results = new List<Dictionary<string, object>>();
            using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;");
            await connection.OpenAsync();

            try {
                using var command = new SqliteCommand("SELECT * FROM LMRX0800;", connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    results.Add(row);
                }
            } catch { }
            return results;
        }

        public async Task<SemanticModelResponse?> GetTicketsSummaryAsync(string companyId)
        {
            string dbPath = DbUtils.GetSafeDbPath(companyId);
            if (!System.IO.File.Exists(dbPath)) return null;

            var dataRecords = new List<TicketSummaryRecord>();
            string connectionString = $"Data Source={dbPath};Mode=ReadOnly;";

            using (var conn = new SqliteConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT 
                        COALESCE(TRIM(category), 'Unknown') AS category, 
                        COALESCE(TRIM(call_status), 'Unknown') AS call_status, 
                        COUNT(call_no) AS ticket_count
                    FROM LMRX0800
                    GROUP BY category, call_status";

                using (var cmd = new SqliteCommand(query, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string cat = reader.IsDBNull(0) ? "Unknown" : reader.GetString(0);
                        string status = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                        int count = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);

                        dataRecords.Add(new TicketSummaryRecord
                        {
                            Category = string.IsNullOrWhiteSpace(cat) ? "Unknown" : cat,
                            CallStatus = string.IsNullOrWhiteSpace(status) ? "Unknown" : status,
                            TicketCount = count
                        });
                    }
                }
            }

            return new SemanticModelResponse
            {
                CompanyId = companyId.ToUpperInvariant(),
                Data = dataRecords
            };
        }

        public async Task<Lmrx0800KpiResponse?> GetTicketKpisAsync(string companyId, string year, string period)
        {
            string dbPath = DbUtils.GetSafeDbPath(companyId);
            if (!System.IO.File.Exists(dbPath)) return null;

            string connectionString = $"Data Source={dbPath};Mode=ReadOnly;";

            using (var conn = new SqliteConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT 
                        COUNT(DISTINCT call_no) AS totalTickets,
                        COUNT(DISTINCT CASE WHEN LOWER(category) = 'complaint' THEN call_no END) AS complaintTickets,
                        COUNT(DISTINCT CASE WHEN LOWER(category) = 'maintenance' THEN call_no END) AS maintenanceTickets,
                        COUNT(DISTINCT CASE WHEN LOWER(category) = 'request' THEN call_no END) AS requestTickets,
                        COUNT(DISTINCT CASE WHEN call_status IN ('Solved', 'Closed') THEN call_no END) AS closedTickets
                    FROM LMRX0800
                    WHERE strftime('%Y', call_date) = $year AND strftime('%m', call_date) = $period";

                using (var cmd = new SqliteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("$year", year);
                    cmd.Parameters.AddWithValue("$period", period);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new Lmrx0800KpiResponse
                            {
                                TotalTickets = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                ComplaintTickets = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                MaintenanceTickets = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                RequestTickets = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                ClosedTickets = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                            };
                        }
                    }
                }
            }
            return new Lmrx0800KpiResponse();
        }
    }
}


