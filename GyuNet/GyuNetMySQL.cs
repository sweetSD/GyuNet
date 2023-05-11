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
            Database = Define.MYSQL_DATABASE
        };
        
        public static async Task<bool> ExecuteNonQuery(string query)
        {
            Debug.Log(ConnectionBuilder.ConnectionString);
            using (var connection = new MySqlConnection(ConnectionBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                if (connection.State == ConnectionState.Open)
                {
                    return await new MySqlCommand(query, connection).ExecuteNonQueryAsync() == 1;
                }
                Debug.LogError($"MySQL 연결 실패: {connection.State}");
            }
            return false;
        }
        
        public static async Task<MySqlDataReader> ExecuteReader(string query)
        {
            using (var connection = new MySqlConnection(ConnectionBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                if (connection.State == ConnectionState.Open)
                {
                    var reader = await new MySqlCommand(query, connection).ExecuteReaderAsync();
                    if (reader.IsClosed)
                    {
                        Debug.LogError("MySQL Reader 생성 실패");
                        return null;
                    }
                    return reader;
                }
                Debug.LogError($"MySQL 연결 실패: {connection.State}");
            }
            return null;
        }

        public static async Task<MySqlTransaction> BeginTransaction()
        {
            using (var connection = new MySqlConnection(ConnectionBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                if (connection.State == ConnectionState.Open)
                {
                    return await connection.BeginTransactionAsync();
                }
                Debug.LogError($"MySQL 연결 실패: {connection.State}");
            }
            return null;
        }
        
        public static async Task Commit(MySqlTransaction transaction)
        {
            using (var connection = new MySqlConnection(ConnectionBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                if (connection.State == ConnectionState.Open)
                {
                    await transaction.CommitAsync();
                    return;
                }
                Debug.LogError($"MySQL 연결 실패: {connection.State}");
            }
        }
    }
}
