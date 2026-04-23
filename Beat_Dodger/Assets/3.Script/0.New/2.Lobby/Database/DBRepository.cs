using System;
using System.Threading.Tasks;
using BeatDodger.Core;
using UnityEngine;
using MySql.Data.MySqlClient;

namespace BeatDodger.Database
{
    /// <summary>
    /// 데이터베이스 직접 접근을 담당하는 레포지토리.
    /// 모든 메서드는 비동기(Task)로 동작하여 메인 스레드 블로킹을 방지한다.
    /// </summary>
    public class DBRepository : IUserRepository
    {
        #region Public Methods

        /// <summary>
        /// DB에서 사용자 데이터를 비동기적으로 로드한다.
        /// </summary>
        /// <param name="userId">조회할 사용자 ID</param>
        /// <returns>UserData 구조체. 존재하지 않으면 기본값(빈 _UserId).</returns>
        public async Task<UserData> LoadUserDataAsync(string userId)
        {
            // TODO: 실제 DB 연동 로직(MySQL/MongoDB 등) 구현 필요
            await Task.Delay(100);

            Debug.Log($"[DBRepository] Loaded data for user: {userId}");

            return new UserData
            {
                _UserId = userId,
                _UserName = $"Player_{userId}",
                _Level = 1,
                _Experience = 0,
                _Currency = 1000
            };
        }

        /// <summary>
        /// 사용자 데이터를 DB에 비동기적으로 저장한다.
        /// </summary>
        /// <param name="data">저장할 데이터</param>
        /// <returns>저장 성공 여부</returns>
        public async Task<bool> SaveUserDataAsync(UserData data)
        {
            // TODO: 실제 DB 저장 쿼리 구현 필요
            await Task.Delay(100);

            Debug.Log($"[DBRepository] Saved data for user: {data._UserId}");
            return true;
        }

        #endregion
    }
}
