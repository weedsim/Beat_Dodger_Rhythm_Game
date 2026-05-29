using System;
using System.Threading.Tasks;
using BeatDodger.Core;
using UnityEngine;
using MySql.Data.MySqlClient;

namespace BeatDodger.Database
{
    /// <summary>
    /// MySQL 데이터베이스 직접 접근을 담당하는 레포지토리.
    /// 모든 메서드는 비동기(Task)로 동작하여 메인 스레드 블로킹을 방지한다.
    /// </summary>
    public class DBRepository : IUserRepository
    {
        #region Variables

        // TODO: ConnectionString을 소스 코드에 직접 작성하지 말 것.
        //       권장 방식: StreamingAssets/db_config.json 파일로 분리 후 .gitignore 처리.
        //       예) { "server": "localhost", "database": "beatdodger", "uid": "root", "pwd": "...", "port": 3306 }
        //       DBRepository 초기화 시 해당 파일을 읽어 ConnectionString을 조립하도록 리팩토링 필요.
        private const string ConnectionString =
            "Server=localhost;Database=beatdodger;UID=root;PWD=1234;CharSet=utf8mb4;Port=3306";

        #endregion

        #region Public Methods

        /// <summary>
        /// userId와 password로 DB 인증을 수행한다.
        /// 일치하는 사용자가 없으면 빈 UserData를 반환한다.
        /// </summary>
        public async Task<UserData> AuthenticateAsync(string userId, string password)
        {
            const string query =
                "SELECT user_id, user_name, volume, sync, input_key, frame_rate " +
                "FROM users " +
                "WHERE user_id = @userId AND password = @password " +
                "LIMIT 1";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    await conn.OpenAsync().ConfigureAwait(false);

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@password", password);

                        using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            if (await reader.ReadAsync().ConfigureAwait(false))
                            {
                                return new UserData
                                {
                                    _UserId    = Convert.ToString(reader["user_id"]),
                                    _UserName  = Convert.ToString(reader["user_name"]),
                                    _Volume    = Convert.ToSingle(reader["volume"]),
                                    _Sync      = Convert.ToSingle(reader["sync"]),
                                    _InputKey  = Convert.ToString(reader["input_key"]),
                                    _FrameRate = Convert.ToInt32(reader["frame_rate"])
                                };
                            }
                        }
                    }
                }

                return default;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBRepository] AuthenticateAsync 실패: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 새 사용자를 DB에 삽입한다.
        /// 아이디 중복(MySQL 오류 코드 1062) 시 false를 반환한다.
        /// </summary>
        public async Task<bool> RegisterUserAsync(string userId, string password, string userName)
        {

            const string query =
                "INSERT INTO users (user_id, password, user_name, volume, sync, input_key, frame_rate) " +
                "VALUES (@userId, @password, @userName, 0.0, 0.0, 'space', 60)";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    await conn.OpenAsync().ConfigureAwait(false);

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        cmd.Parameters.AddWithValue("@password", password);
                        cmd.Parameters.AddWithValue("@userName", userName);

                        int rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

                        Debug.Log($"[DBRepository] 회원가입 완료: {userId}");
                        return rows > 0;
                    }
                }
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                Debug.LogWarning($"[DBRepository] 이미 존재하는 아이디: {userId}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBRepository] RegisterUserAsync 실패: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
