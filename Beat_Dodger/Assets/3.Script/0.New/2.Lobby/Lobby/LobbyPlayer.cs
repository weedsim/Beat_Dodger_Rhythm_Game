using UnityEngine;
using Mirror;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 로비에서 네트워크 상에 존재하는 플레이어 오브젝트.
    /// 서버에서 스폰되며 userId / userName 을 모든 클라이언트에 동기화한다.
    /// </summary>
    public class LobbyPlayer : NetworkBehaviour
    {
        #region Variables

        [SyncVar]
        private string _userId;

        [SyncVar]
        private string _userName;

        [SyncVar(hook = nameof(OnCurrentPartyIdChanged))]
        private int _currentPartyId;

        /// <summary>
        /// 로컬 플레이어의 파티 ID가 변경될 때 발행되는 정적 이벤트.
        /// LobbyUIManager 등이 구독하여 UI 패널 전환에 활용한다.
        /// </summary>
        public static event System.Action<int> OnLocalPartyIdChanged;

        #endregion

        #region Properties

        /// <summary>
        /// 이 플레이어의 고유 사용자 ID.
        /// </summary>
        public string UserId
        {
            get
            {
                return _userId;
            }
        }

        /// <summary>
        /// 이 플레이어의 표시 이름.
        /// </summary>
        public string UserName
        {
            get
            {
                return _userName;
            }
        }

        /// <summary>
        /// 이 플레이어가 현재 속한 파티 ID. 0이면 방에 없는 상태.
        /// </summary>
        public int CurrentPartyId
        {
            get
            {
                return _currentPartyId;
            }
        }

        #endregion

        #region Unity Lifecycle Methods

        private void OnDisable()
        {
            if (isServer || isClient)
            {
                Debug.LogWarning($"[LobbyPlayer] {gameObject.name} 가 비활성화되었습니다.\n{System.Environment.StackTrace}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 서버에서 스폰 직후 플레이어 식별 정보를 설정한다. 서버 전용.
        /// </summary>
        /// <param name="userId">플레이어의 고유 사용자 ID</param>
        /// <param name="userName">플레이어의 표시 이름</param>
        [Server]
        public void SetIdentity(string userId, string userName)
        {
            _userId = userId;
            _userName = userName;
        }

        /// <summary>
        /// 플레이어가 속한 파티 ID를 설정한다. 서버 전용. 0이면 방에 없는 상태.
        /// </summary>
        /// <param name="partyId">설정할 파티 ID. 0이면 방 없음.</param>
        [Server]
        public void SetCurrentPartyId(int partyId)
        {
            _currentPartyId = partyId;
        }

        #endregion

        #region Private Methods

        private void OnCurrentPartyIdChanged(int oldValue, int newValue)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            OnLocalPartyIdChanged?.Invoke(newValue);
        }

        #endregion
    }
}
