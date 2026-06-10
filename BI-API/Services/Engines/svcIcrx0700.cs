using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bimasakti.BiService.Api.Core;

namespace Bimasakti.BiService.Api.Services.Engines
{
    public interface IsvcIcrx0700
    {
        Task<object> GetDataAsync(string companyId);
    }

    public class svcIcrx0700 : IsvcIcrx0700
    {
        public async Task<object> GetDataAsync(string companyId)
        {
            string databasePath = DbUtils.GetSafeDbPath(companyId);
            if (!System.IO.File.Exists(databasePath)) return new List<object>();

            var results = new List<Dictionary<string, object?>>();
            using var connection = new SqliteConnection("Data Source=$databasePath;Mode=ReadOnly;");
            await connection.OpenAsync();

            try {
                using var command = new SqliteCommand("SELECT * FROM ICRX0700;", connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    results.Add(row);
                }
            } catch { }
            return results;
        }
    }
}


