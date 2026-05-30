using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Authorization;

namespace BiPortal.FinancialReports.Backend.Engines
{
    [Authorize]
    [ApiController]
    [Route("api/engine/lm")]
    public class dsLmController : ControllerBase
    {
        public class TicketSummaryRecord
        {
            public string Category { get; set; } = "Unknown";
            public string CallStatus { get; set; } = "Unknown";
            public int TicketCount { get; set; }
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

        public class Lmrx0800KpiResponse
        {
            public int TotalTickets { get; set; }
            public int ComplaintTickets { get; set; }
            public int MaintenanceTickets { get; set; }
            public int RequestTickets { get; set; }
            public int ClosedTickets { get; set; }
        }

        [HttpGet("tickets/summary/{companyId}")]
        public IActionResult GetTicketsSummary([FromRoute] string companyId)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !companyId.Equals(companyIdClaim, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { detail = "Access to requested company database is denied." });
            }

            string dbPath = svcDbUtils.GetSafeDbPath(companyId);

            if (!System.IO.File.Exists(dbPath))
            {
                return NotFound(new { detail = $"Database for company '{companyId}' not found." });
            }

            string connectionString = $"Data Source={dbPath};Mode=ReadOnly;";
            var dataRecords = new List<TicketSummaryRecord>();

            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            COALESCE(TRIM(category), 'Unknown') AS category, 
                            COALESCE(TRIM(call_status), 'Unknown') AS call_status, 
                            COUNT(call_no) AS ticket_count
                        FROM LMRX0800
                        GROUP BY category, call_status";

                    using (var cmd = new SqliteCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
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
                }

                var response = new SemanticModelResponse
                {
                    CompanyId = companyId.ToUpperInvariant(),
                    Data = dataRecords
                };

                return Ok(response);
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { detail = "The tickets table (LMRX0800) does not exist for this company." });
            }
            catch (SqliteException)
            {
                return StatusCode(500, new { detail = "A database operational error occurred while processing the request." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { detail = "An unexpected error occurred while compiling the semantic ticket dataset." });
            }
        }

        [HttpGet("/api/engine/lmrx0800/kpi/{companyId}")]
        public IActionResult GetTicketKpis(
            [FromRoute] string companyId,
            [FromQuery] string year,
            [FromQuery] string period)
        {
            var companyIdClaim = HttpContext.User.FindFirst("company_id")?.Value;
            if (string.IsNullOrEmpty(companyIdClaim) || !companyId.Equals(companyIdClaim, StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { detail = "Access to requested company database is denied." });
            }

            string dbPath = svcDbUtils.GetSafeDbPath(companyId);

            if (!System.IO.File.Exists(dbPath))
            {
                return NotFound(new { detail = $"Database for company '{companyId}' not found." });
            }

            // Connect in read-only mode for safety and concurrency
            string connectionString = $"Data Source={dbPath};Mode=ReadOnly;";

            try
            {
                using (var conn = new SqliteConnection(connectionString))
                {
                    conn.Open();

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

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var response = new Lmrx0800KpiResponse
                                {
                                    TotalTickets = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                                    ComplaintTickets = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                                    MaintenanceTickets = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    RequestTickets = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                    ClosedTickets = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
                                };
                                return Ok(response);
                            }
                            else
                            {
                                return Ok(new Lmrx0800KpiResponse());
                            }
                        }
                    }
                }
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { detail = "The tickets table (LMRX0800) does not exist for this company." });
            }
            catch (SqliteException)
            {
                return StatusCode(500, new { detail = "A database operational error occurred." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { detail = "An unexpected error occurred while fetching ticket KPIs." });
            }
        }
    }
}
