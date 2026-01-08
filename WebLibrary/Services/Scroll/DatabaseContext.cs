using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System;

namespace Scroll.Database
{
    public class DatabaseContext : IDisposable
    {
        private OracleConnection _connection;
        private readonly string _connectionString;

        public DatabaseContext(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("OracleDb");
        }

        public DatabaseContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public OracleConnection GetConnection()
        {
            if (_connection == null)
            {
                _connection = new OracleConnection(_connectionString);
            }

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
            }

            return _connection;
        }

        public async Task<OracleConnection> GetConnectionAsync()
        {
            if (_connection == null)
            {
                _connection = new OracleConnection(_connectionString);
            }

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            return _connection;
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                if (_connection.State != System.Data.ConnectionState.Closed)
                {
                    _connection.Close();
                }

                _connection.Dispose();
                _connection = null;
            }
        }
    }

}
