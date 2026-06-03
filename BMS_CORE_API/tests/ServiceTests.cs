using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Data.Sqlite;
using BMS_BI_SERVICE.Core.Services;
using BMS_BI_SERVICE.Core.Engines;

namespace BMS_BI_SERVICE.Core.Tests
{
    // --- 1. AUTHENTICATION SERVICE TESTS ---

    public class AuthenticationServiceTests
    {
        private readonly IsvcAuthenticationService _authenticationService = new svcAuthenticationService();

        [Fact]
        public void GetPasswordHash_ShouldReturnValidSha256HexString()
        {
            // Arrange
            string plainTextPassword = "admin123";
            string expectedSha256Hash = "240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9";

            // Act
            string actualHash = _authenticationService.GetPasswordHash(plainTextPassword);

            // Assert
            Assert.Equal(expectedSha256Hash, actualHash);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnTrueForMatchingPassword()
        {
            // Arrange
            string plainTextPassword = "mySecretPassword";
            string hashedPassword = _authenticationService.GetPasswordHash(plainTextPassword);

            // Act
            bool isVerified = _authenticationService.VerifyPassword(plainTextPassword, hashedPassword);

            // Assert
            Assert.True(isVerified);
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalseForNonMatchingPassword()
        {
            // Arrange
            string correctPassword = "mySecretPassword";
            string incorrectPassword = "wrongPassword";
            string hashedPasswordOfCorrectPassword = _authenticationService.GetPasswordHash(correctPassword);

            // Act
            bool isVerified = _authenticationService.VerifyPassword(incorrectPassword, hashedPasswordOfCorrectPassword);

            // Assert
            Assert.False(isVerified);
        }

        [Fact]
        public void CreateAccessToken_ShouldGenerateNonEmptyJwtToken()
        {
            // Arrange
            Dictionary<string, string> claimMap = new()
            {
                { "sub", "admin" },
                { "company_id", "ASHMD" }
            };
            string secretKey = "super-secret-production-key-change-me-for-testing-purposes";
            int expireMinutes = 30;

            // Act
            string token = _authenticationService.CreateAccessToken(claimMap, secretKey, expireMinutes);

            // Assert
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.Contains(".", token); // Standard JWT token format segment separator
        }
    }

    // --- 2. LEDGER SERVICE TESTS ---

    public class svcGlrx0310Tests
    {
        private readonly IsvcGlrx0310 _ledgerService = new svcGlrx0310();

        [Fact]
        public async Task GenerateLedgerReportAsync_ShouldReturnErrorResponse_WhenDatabaseFileDoesNotExist()
        {
            // Arrange
            string nonExistentDbPath = "non_existent_database_file.db";
            string companyId = "ASHMD";
            string preset = "preset1";

            // Act
            var result = await _ledgerService.GenerateLedgerReportAsync(nonExistentDbPath, "2026", "05", preset, companyId);

            // Assert
            Assert.Equal("error", result.Status);
            Assert.Equal("Database file not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task GenerateLedgerReportAsync_ShouldProcessSuccessfully_WithInmemoryOrEmptyTable()
        {
            // Arrange
            string temporaryDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            
            try
            {
                // Create empty schema in temp DB to test parsing
                using var connection = new SqliteConnection($"Data Source={temporaryDbPath}");
                await connection.OpenAsync();
                
                // Create GLRX0310 table schema
                string createTableQuery = @"
                    CREATE TABLE GLRX0310 (
                        company_id TEXT,
                        account_no TEXT,
                        account_name TEXT,
                        year TEXT,
                        period TEXT,
                        end_bsis REAL,
                        end_balance REAL,
                        end_budget REAL
                    );
                    CREATE TABLE coa_mappings (
                        company_id TEXT,
                        preset_name TEXT,
                        mapping_data TEXT
                    );";
                
                using var command = new SqliteCommand(createTableQuery, connection);
                await command.ExecuteNonQueryAsync();

                // Insert sample ledger data for Assets (account starting with '1') and Equity (starting with '3')
                string insertQuery = @"
                    INSERT INTO GLRX0310 (company_id, account_no, account_name, year, period, end_bsis, end_balance)
                    VALUES ('ASHMD', '10101', 'Cash in Bank', '2026', '05', 15000.0, 15000.0),
                           ('ASHMD', '30101', 'Capital Stock', '2026', '05', -10000.0, 10000.0);";
                
                using var insertCmd = new SqliteCommand(insertQuery, connection);
                await insertCmd.ExecuteNonQueryAsync();

                // Act
                var report = await _ledgerService.GenerateLedgerReportAsync(temporaryDbPath, "2026", "05", "preset1", "ASHMD");

                // Assert
                Assert.Equal("success", report.Status);
                Assert.True(report.Data.ContainsKey("Assets"));
                Assert.True(report.Data.ContainsKey("Equity"));
                
                // Assert sign calculation rules
                // Assets (10101 starts with 1) => positive 15000
                Assert.Equal(15000.0m, report.Data["Assets"].Total);
                // Equity (30101 starts with 3) => end_bsis * -1 => -10000 * -1 => positive 10000
                Assert.Equal(10000.0m, report.Data["Equity"].Total);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (System.IO.File.Exists(temporaryDbPath))
                {
                    System.IO.File.Delete(temporaryDbPath);
                }
            }
        }

        [Fact]
        public async Task GenerateLedgerReportAsync_ShouldProcessSuccessfully_WithGLRX0300SchemaAndAlternateColumnNames()
        {
            // Arrange
            string temporaryDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            
            try
            {
                using var connection = new SqliteConnection($"Data Source={temporaryDbPath}");
                await connection.OpenAsync();
                
                // Create GLRX0300 table schema (alternate names like ending_balance, year_period, month_period)
                string createTableQuery = @"
                    CREATE TABLE GLRX0300 (
                        company_id TEXT,
                        account_no TEXT,
                        account_name TEXT,
                        year_period TEXT,
                        month_period TEXT,
                        ending_bsis REAL,
                        ending_balance REAL,
                        ending_budget REAL
                    );";
                
                using var command = new SqliteCommand(createTableQuery, connection);
                await command.ExecuteNonQueryAsync();

                // Insert sample ledger data using GLRX0300 column names
                string insertQuery = @"
                    INSERT INTO GLRX0300 (company_id, account_no, account_name, year_period, month_period, ending_bsis, ending_balance, ending_budget)
                    VALUES ('PCGR1', '10101', 'Cash in Bank', '2026', '05', 15000.0, 15000.0, 1000.0),
                           ('PCGR1', '30101', 'Capital Stock', '2026', '05', -10000.0, 10000.0, 0.0);";
                
                using var insertCommand = new SqliteCommand(insertQuery, connection);
                await insertCommand.ExecuteNonQueryAsync();

                // Act
                var report = await _ledgerService.GenerateLedgerReportAsync(temporaryDbPath, "2026", "05", "preset1", "PCGR1");

                // Assert
                Assert.Equal("success", report.Status);
                Assert.True(report.Data.ContainsKey("Assets"));
                Assert.True(report.Data.ContainsKey("Equity"));
                
                // Assert sign calculation rules
                Assert.Equal(15000.0m, report.Data["Assets"].Total);
                Assert.Equal(10000.0m, report.Data["Equity"].Total);

                // Assert ending budget was read correctly from ending_budget
                var assetItem = report.Data["Assets"].Groups["Uncategorized"].Items[0];
                Assert.Equal(1000.0m, assetItem.EndBudget);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (System.IO.File.Exists(temporaryDbPath))
                {
                    System.IO.File.Delete(temporaryDbPath);
                }
            }
        }


        [Fact]
        public async Task VerifyRealDatabaseSyncAndLedgerReport_ShouldReturnSuccessAndPopulatedData()
        {
            // Arrange
            // Get path strictly using svcDbUtils.GetSafeDbPath which we refactored
            string dbPath = svcDbUtils.GetSafeDbPath("ASHMD");
            string companyId = "ASHMD";
            string preset = "preset1";

            // Act
            // Determine years and periods dynamically from the database
            List<string> yearsList = new();
            List<string> periodsList = new();
            using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
            await connection.OpenAsync();
            
            // Assert the GLRX0310 table actually has data rows
            using (var countCmd = new SqliteCommand("SELECT COUNT(*) FROM GLRX0310;", connection))
            {
                long rowCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
                Assert.True(rowCount > 0, $"GLRX0310 table in {dbPath} is empty!");
            }

            // Query distinct years
            using (var command = new SqliteCommand("SELECT DISTINCT year FROM GLRX0310 WHERE year IS NOT NULL AND year != '';", connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) yearsList.Add(reader.GetString(0));
            }

            // Query distinct periods
            using (var command = new SqliteCommand("SELECT DISTINCT period FROM GLRX0310 WHERE period IS NOT NULL AND period != '';", connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) periodsList.Add(reader.GetString(0));
            }

            Assert.NotEmpty(yearsList);
            Assert.NotEmpty(periodsList);

            string targetYear = yearsList[0];
            string targetPeriod = periodsList[periodsList.Count - 1];

            // Act: Generate ledger report
            var report = await _ledgerService.GenerateLedgerReportAsync(dbPath, targetYear, targetPeriod, preset, companyId);

            // Assert
            Assert.Equal("success", report.Status);
            Assert.NotNull(report.Data);
            Assert.True(report.Data.Count > 0, "Ledger report data is empty!");
            
            // Check that Assets or Liabilities or Equity have been populated
            bool hasAssets = report.Data.ContainsKey("Assets") && report.Data["Assets"].Total != 0;
            bool hasLiabilities = report.Data.ContainsKey("Liabilities") && report.Data["Liabilities"].Total != 0;
            bool hasEquity = report.Data.ContainsKey("Equity") && report.Data["Equity"].Total != 0;
            
            Assert.True(hasAssets || hasLiabilities || hasEquity, "All ledger report sections returned zero totals!");
        }
    }

    // --- 3. DASHBOARD ANALYTICS SERVICE TESTS ---

    public class DashboardAnalyticsServiceTests
    {
        private readonly IsvcDashboardAnalyticsService _dashboardAnalyticsService = new svcDashboardAnalyticsService();

        [Fact]
        public async Task GetOperationsMetricsAsync_ShouldReturnDefaultKPIs_WhenDatabaseDoesNotExist()
        {
            // Arrange
            string nonExistentDbPath = "missing_operations_database.db";
            string companyId = "ASHMD";

            // Act
            var metrics = await _dashboardAnalyticsService.GetOperationsMetricsAsync(nonExistentDbPath, companyId);

            // Assert
            Assert.NotNull(metrics);
            Assert.Equal(1306, metrics.ActiveTenants);
            Assert.Equal(87.4, metrics.OccupancyRate);
            Assert.Equal(7, metrics.DailyFootTraffic.Count);
            Assert.Equal(6, metrics.LeaseExpirationsTimeline.Count);
        }

        [Fact]
        public async Task GetMaintenanceStatusAsync_ShouldReturnDefaultStatus_WhenDatabaseDoesNotExist()
        {
            // Arrange
            string nonExistentDbPath = "missing_maintenance_database.db";
            string companyId = "ASHMD";

            // Act
            var status = await _dashboardAnalyticsService.GetMaintenanceStatusAsync(nonExistentDbPath, companyId);

            // Assert
            Assert.NotNull(status);
            Assert.Equal(59, status.OpenTickets);
            Assert.Equal(2, status.CriticalAlerts);
            Assert.Equal(4, status.EquipmentUptimePercent.Count);
            Assert.Equal(5, status.TicketsByCategory.Count);
        }
    }
}
