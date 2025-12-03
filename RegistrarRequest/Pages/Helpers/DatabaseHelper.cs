using MySql.Data.MySqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;

namespace ProjectCapstone.Helpers
{
    public class DatabaseHelper
    {
        private readonly string? _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            }
        }

        public MySqlConnection GetConnection()
        {
            // _connectionString validated in constructor
            return new MySqlConnection(_connectionString!);
        }

        public async Task<DataTable> ExecuteQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            using var adapter = new MySqlDataAdapter(command);
            var dataTable = new DataTable();
            adapter.Fill(dataTable);

            return dataTable;
        }

        public async Task<int> ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object?> ExecuteScalarAsync(string query, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();

            using var command = new MySqlCommand(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            return await command.ExecuteScalarAsync();
        }
    }
}