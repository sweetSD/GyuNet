using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;
namespace GyuNet
{
    public static class GyuNetMySQL
    {
        private static readonly MySqlConnectionStringBuilder ConnectionBuilder = new MySqlConnectionStringBuilder
        {
            UserID = Define.MYSQL_UID,
            Password = Define.MYSQL_PASSWORD,
            Server = Define.MYSQL_SERVER,
            Database = Define.MYSQL_DATABASE,
            Pooling = true,
            MinimumPoolSize = 10,
            MaximumPoolSize = 1000,
        };

        public static MySqlConnection CreateConnection() => new MySqlConnection(ConnectionBuilder.ConnectionString);
        
        public static async Task<bool> ExecuteNonQuery(string query, List<(string Name, string Value)> parameters = null)
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("MySQL 연결 실패!");
                }
                using (var command = new MySqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var parameter in parameters)
                        {
                            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
                        }
                    }
                    return await command.ExecuteNonQueryAsync() == 1;
                }
            }
        }

        public static async Task<MySqlDataReader> ExecuteReader(string query, List<(string Name, string Value)> parameters = null)
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("MySQL 연결 실패!");
                }
                using (var command = new MySqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        foreach (var parameter in parameters)
                        {
                            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
                        }
                    }

                    var reader = await command.ExecuteReaderAsync();
                    if (reader == null || reader.IsClosed)
                    {
                        throw new Exception("MySQL Reader 생성 실패!");
                    }
                    return reader;
                }
            }
        }

        public static async Task<MySqlTransaction> BeginTransaction()
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("MySQL 연결 실패!");
                }
                return await connection.BeginTransactionAsync();
            }
        }

        public static async Task Commit(MySqlTransaction transaction)
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("MySQL 연결 실패!");
                }
                await transaction.CommitAsync();
            }
        }
    }
}
