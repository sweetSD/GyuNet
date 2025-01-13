using System;
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
        
        public static async Task<bool> ExecuteNonQuery(string query)
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State == ConnectionState.Open)
                {
                    return await new MySqlCommand(query, connection).ExecuteNonQueryAsync() == 1;
                }
                throw new Exception("MySQL 연결 실패!");
            }
        }

        public static async Task ExecuteReader(string query, Action<MySqlDataReader> callback)
        {
            using (var connection = CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("MySQL 연결 실패!");
                }
                var reader = await new MySqlCommand(query, connection).ExecuteReaderAsync();
                if (reader == null || reader.IsClosed)
                {
                    throw new Exception("MySQL Reader 생성 실패!");
                }
                callback?.Invoke(reader);
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
