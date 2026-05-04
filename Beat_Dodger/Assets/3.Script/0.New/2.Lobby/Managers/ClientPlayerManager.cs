using UnityEngine;
using BeatDodger.Core;

namespace BeatDodger.Managers
{
    /// <summary>
    /// 로그인 성공 후 서버로부터 받은 유저 정보를 로컬에 유지하는 매니저
    /// </summary>
    public class ClientPlayerManager : MonoBehaviour
    {
        #region Variables

        private UserData _localUserData;

        #endregion

        #region Properties

        /// <summary>
        /// 현재 로그인된 유저의 데이터 정보를 제공한다.
        /// </summary>
        public UserData LocalUserData
        {
            get
            {
                return _localUserData;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 서버로부터 받은 유저 정보를 로컬 세션에 저장한다.
        /// </summary>
        /// <param name="userData">저장할 유저 데이터</param>
        public void SetUserData(UserData userData)
        {
            _localUserData = userData;
            Debug.Log($"[ClientPlayerManager] User data cached: {_localUserData._UserName}");
        }

        /// <summary>
        /// 로그아웃 시 로컬 유저 정보를 초기화한다.
        /// </summary>
        public void ClearUserData()
        {
            _localUserData = default;
        }

        #endregion
    }
}