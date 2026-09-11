using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using MySqlConnector;

namespace GoodGovernanceApp.Data
{
    public class DatabaseHelper
    {
        private readonly IDatabaseConfig _databaseConfig;

        public DatabaseHelper(IDatabaseConfig databaseConfig)
        {
            _databaseConfig = databaseConfig;
        }

        public async Task<MySqlConnection> OpenConnectionAsync()
        {
            var builder = new MySqlConnectionStringBuilder(_databaseConfig.ConnectionString)
            {
                ConnectionTimeout = 15,
                DefaultCommandTimeout = 30
            };
            MySqlConnection connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task<(bool IsSuccess, string Message)> TestConnectionAsync(string? connectionStringOverride = null)
        {
            try
            {
                string connectionString = connectionStringOverride ?? _databaseConfig.ConnectionString;
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return (true, "Online connection successful!");
                }
            }
            catch (MySqlException ex)
            {
                return (false, $"MySQL Error [{ex.Number}]: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Connection Error: {ex.Message}");
            }
        }

        public async Task<DataTable> ExecuteQueryAsync(string query, params SqliteParameter[] parameters)
        {
            using (var connection = await OpenConnectionAsync())
            {
                using (var command = new MySqlCommand(query, connection))
                {
                    AddParameters(command, parameters);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        DataTable dataTable = new DataTable();
                        dataTable.Load(reader);
                        return dataTable;
                    }
                }
            }
        }

        public async Task<int> ExecuteNonQueryAsync(string query, params SqliteParameter[] parameters)
        {
            using (var connection = await OpenConnectionAsync())
            {
                using (var command = new MySqlCommand(query, connection))
                {
                    AddParameters(command, parameters);

                    return await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<object?> ExecuteScalarAsync(string query, params SqliteParameter[] parameters)
        {
            using (var connection = await OpenConnectionAsync())
            {
                using (var command = new MySqlCommand(query, connection))
                {
                    AddParameters(command, parameters);

                    var result = await command.ExecuteScalarAsync();
                    return result == DBNull.Value ? null : result;
                }
            }
        }

        private static void AddParameters(MySqlCommand command, SqliteParameter[] parameters)
        {
            if (parameters == null)
                return;

            foreach (var parameter in parameters)
                command.Parameters.AddWithValue(parameter.ParameterName, parameter.Value ?? DBNull.Value);
        }
    }
}
