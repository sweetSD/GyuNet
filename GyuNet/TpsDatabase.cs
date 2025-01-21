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
            var query = $"SELECT * FROM user WHERE Name = @id {(string.IsNullOrEmpty(pw) ? string.Empty : "AND Password = @pw")}";
            var duplicated = false;

            using (var reader = await GyuNetMySQL.ExecuteReader(query, ("@id", name), ("@pw", pw))) 
            {
                duplicated = reader.HasRows;
            }
            return duplicated;
        }

        // 새로운 유저 정보를 만듭니다.
        public static async Task CreateNewUser(string name, string pw)
        {
            var query = "INSERT INTO user (Name, Password) VALUES (@id, @pw)";
            await GyuNetMySQL.ExecuteNonQuery(query, ("@id", name), ("@pw", pw));
        }

        // 새로운 게임 기록 정보를 만듭니다.
        public static async Task<bool> CreateNewRecord(string userName, int killCount)
        {
            var query = $"SELECT ID FROM user WHERE Name = @user";

            using (var reader = await GyuNetMySQL.ExecuteReader(query,  ("@user", userName)))
            {
                if (reader.HasRows == false)
                {
                    return false;
                }
                reader.Read();
                var id = reader["id"];
                await GyuNetMySQL.ExecuteNonQuery($"INSERT INTO fps_record (UserId, KillCount) VALUES ({id}, {killCount})");
            }

            return true;
        }

        // 랭킹 데이터를 가져옵니다.
        public static async Task<List<(int Index, string Name, int KillCount)>> GetRank()
        {
            var query = @"
                SELECT u.ID, u.Name, r.KillCount
                FROM user u
                JOIN(
                  SELECT UserID, SUM(KillCount) AS KillCount
                  FROM fps_record
                  GROUP BY UserID
                ) r ON u.ID = r.UserID
                ORDER BY r.KillCount DESC; ";
            var rankData = new List<(int, string, int)>();
            
            using (var reader = await GyuNetMySQL.ExecuteReader(query))
            {
                var index = 1;
                while (reader.Read() && index <= 100)
                {
                    rankData.Add((index++, reader["Name"].ToString(), reader.GetInt32("KillCount")));
                }
            }
            return rankData;
        }

        // 개인 기록 데이터를 가져옵니다.
        public static async Task<List<(string Name, int KillCount, string Date)>> GetRecord(string name)
        {
            var query = $@"
                SELECT user.Name, fps_record.KillCount, fps_record.CreatedAt 
                FROM user 
                JOIN fps_record 
                ON user.id = fps_record.userid 
                WHERE user.name = '{name}';";
            var rankData = new List<(string, int, string)>();
            
            using (var reader = await GyuNetMySQL.ExecuteReader(query))
            {
                while (reader.Read())
                {
                    rankData.Add((reader["Name"].ToString(), reader.GetInt32("KillCount"), reader.GetDateTime("CreatedAt").ToString("yyyy-MM-dd_HH+mm+ss")));
                }
            }
            return rankData;
        }
    }
}