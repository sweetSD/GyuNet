using System;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;
namespace GyuNet
{
    public class GyuNetMySQL
    {
        private readonly MySqlConnectionStringBuilder connectionBuilder;
        
        public GyuNetMySQL(string server = "localhost", string database = "game_db", string uid = "root", string password = "root")
        {
            connectionBuilder = new MySqlConnectionStringBuilder
            {
                UserID = uid,
                Password = password,
                Server = server,
                Database = database
            };
        }

        public async Task<bool> ExecuteNonQuery(string query)
        {
            Debug.Log(connectionBuilder.ConnectionString);
            using (var connection = new MySqlConnection(connectionBuilder.ConnectionString))
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
        
        public async Task<MySqlDataReader> ExecuteReader(string query)
        {
            using (var connection = new MySqlConnection(connectionBuilder.ConnectionString))
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
    }
}
