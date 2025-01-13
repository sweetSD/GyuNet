using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySqlConnector;

namespace GyuNet
{
    public static class TpsDatabase
    {
        public static async Task<bool> CheckAccount(string name, string pw = null)
        {
            var duplicated = false;
            using (var connection = GyuNetMySQL.CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("Failed connect to database.");
                }

                var query = $"SELECT * FROM user WHERE Name = @id {(string.IsNullOrEmpty(pw) ? string.Empty : "AND Password = @pw")}";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@id", name);
                    cmd.Parameters.AddWithValue("@pw", pw);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (reader == null || reader.IsClosed)
                        {
                            throw new Exception("Failed open reader.");
                        }
                        duplicated = reader.HasRows;
                    }
                }
            }
            return duplicated;
        }

        // 새로운 유저 정보를 만듭니다.
        public static async Task CreateNewUser(string name, string pw)
        {
            using (var connection = GyuNetMySQL.CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("Failed connect to database.");
                }

                var query = "INSERT INTO user (Name, Password) VALUES (@id, @pw)";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@id", name);
                    cmd.Parameters.AddWithValue("@pw", pw);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // 새로운 게임 기록 정보를 만듭니다.
        public static async Task CreateNewRecord(string userName, int killCount)
        {
            using (var connection = GyuNetMySQL.CreateConnection())
            {
                await connection.OpenAsync();
                if (connection.State != ConnectionState.Open)
                {
                    throw new Exception("Failed connect to database.");
                }

                var query = $"SELECT ID FROM user WHERE Name = @user";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@user", userName);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (reader == null || reader.IsClosed)
                        {
                            throw new Exception("Failed open reader.");
                        }

                        if (reader.HasRows == false)
                        {
                            return;
                        }
                        
                        reader.Read();
                        var id = reader["id"];
                        await GyuNetMySQL.ExecuteNonQuery($"INSERT INTO fps_record (UserId, KillCount) VALUES ({id}, {killCount})");
                    }
                }
            }
        }

        // 랭킹 데이터를 가져옵니다.
        public static async Task<List<(int Index, string Name, int KillCount)>> GetRank()
        {
            List<(int, string, int)> rankData = new List<(int, string, int)>();
            var query = @"
                SELECT u.ID, u.Name, r.KillCount
                FROM user u
                JOIN(
                  SELECT UserID, SUM(KillCount) AS KillCount
                  FROM fps_record
                  GROUP BY UserID
                ) r ON u.ID = r.UserID
                ORDER BY r.KillCount DESC; ";
            var index = 1;
            await GyuNetMySQL.ExecuteReader(query,
                reader => {
                    while (reader.Read() && index <= 100)
                    {
                        rankData.Add((index++, reader["Name"].ToString(), reader.GetInt32("KillCount")));
                    }
                });
            return rankData;
        }

        // 개인 기록 데이터를 가져옵니다.
        public static async Task<List<(string Name, int KillCount, string Date)>> GetRecord(string name)
        {
            List<(string, int, string)> rankData = new List<(string, int, string)>();
            var query = $@"
                SELECT user.Name, fps_record.KillCount, fps_record.CreatedAt 
                FROM user 
                JOIN fps_record 
                ON user.id = fps_record.userid 
                WHERE user.name = '{name}';";
            var index = 1;
            await GyuNetMySQL.ExecuteReader(query,
                reader => {
                    while (reader.Read() && index <= 100)
                    {
                            rankData.Add((reader["Name"].ToString(), reader.GetInt32("KillCount"), reader.GetDateTime("CreatedAt").ToString("yyyy-MM-dd_HH+mm+ss")));
                    }
                });
            return rankData;
        }
    }
}