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
    public class DBService : IDBService
    {
        #region Variables

        private readonly IUserRepository _repository;

        #endregion

        #region Constructor

        public DBService(IUserRepository repository)
        {
            _repository = repository;
        }

        #endregion

        #region Public Methods

        public async Task<UserData> AuthenticateUserAsync(string userId, string password)
        {
            try
            {
                return await _repository.AuthenticateAsync(userId, password);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBService] AuthenticateUserAsync 실패: {ex.Message}");
                return default;
            }
        }

        public async Task<bool> RegisterUserAsync(string userId, string password, string userName)
        {
            try
            {
                return await _repository.RegisterUserAsync(userId, password, userName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DBService] RegisterUserAsync 실패: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
