using System;
using System.Threading.Tasks;
using BeatDodger.Core;
using BeatDodger.Database;
using UnityEngine;

namespace BeatDodger.Services
{
    /// <summary>
    /// DB 레포지토리를 사용하여 비즈니스 로직을 처리하는 서비스 클래스.
    /// </summary>
    public class DBService
    {
        #region Variables

        private readonly IUserRepository _repository;

        #endregion

        #region Constructor

        /// <summary>
        /// IUserRepository 구현체를 주입받아 DBService를 초기화한다.
        /// </summary>
        /// <param name="repository">사용자 데이터 저장소 구현체</param>
        public DBService(IUserRepository repository)
        {
            _repository = repository;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 유저의 데이터를 가져오며, 데이터가 없을 경우 기본 데이터를 생성하여 반환한다.
        /// </summary>
        /// <param name="userId">조회할 사용자 ID</param>
        /// <returns>로드 또는 신규 생성된 UserData. 오류 시 기본값(빈 구조체).</returns>
        public async Task<UserData> GetUserOrCreateAsync(string userId)
        {
            try
            {
                UserData data = await _repository.LoadUserDataAsync(userId);

                if (string.IsNullOrEmpty(data._UserId))
                {
                    UserData newUser = new UserData { _UserId = userId, _UserName = "NewPlayer" };
                    await _repository.SaveUserDataAsync(newUser);
                    return newUser;
                }

                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBService] Failed to get user data: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 게임 종료 후 유저의 점수와 경험치를 비동기적으로 업데이트한다.
        /// </summary>
        /// <param name="userId">대상 사용자 ID</param>
        /// <param name="addExp">추가할 경험치</param>
        /// <param name="addCurrency">추가할 재화</param>
        public async Task UpdateUserProgressAsync(string userId, int addExp, int addCurrency)
        {
            UserData data = await _repository.LoadUserDataAsync(userId);
            data._Experience += addExp;
            data._Currency += addCurrency;

            await _repository.SaveUserDataAsync(data);
        }

        #endregion
    }
}
