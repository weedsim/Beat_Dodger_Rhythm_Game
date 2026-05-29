using BeatDodger.Core;

namespace BeatDodger.Sessions
{
    /// <summary>
    /// 서버에서 관리하는 개별 플레이어의 세션 정보
    /// </summary>
    public class PlayerSessionInfo
    {
        #region Variables

        private string _userId;
        private string _userName;
        private SessionState _currentState;
        private int _matchId;
        private uint _netId;

        #endregion

        #region Properties

        /// <summary>
        /// 플레이어의 고유 사용자 ID. 접속 초기에는 임시값이었다가 로그인 성공 시 DB 값으로 교체된다.
        /// </summary>
        public string UserId
        {
            get
            {
                return _userId;
            }
            set
            {
                _userId = value;
            }
        }

        /// <summary>
        /// DB에서 로드된 플레이어의 표시용 이름. 로그인 성공 전에는 빈 문자열이다.
        /// </summary>
        public string UserName
        {
            get
            {
                return _userName;
            }
            set
            {
                _userName = value;
            }
        }

        /// <summary>
        /// 플레이어의 현재 세션 상태.
        /// </summary>
        public SessionState CurrentState
        {
            get
            {
                return _currentState;
            }
            set
            {
                _currentState = value;
            }
        }

        /// <summary>
        /// 플레이어가 속한 매치 ID. 미배정 시 -1.
        /// </summary>
        public int MatchId
        {
            get
            {
                return _matchId;
            }
            set
            {
                _matchId = value;
            }
        }

        /// <summary>
        /// 스폰된 LobbyPlayer NetworkBehaviour 의 netId. 스폰 전에는 0.
        /// </summary>
        public uint NetId
        {
            get
            {
                return _netId;
            }
            set
            {
                _netId = value;
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// PlayerSessionInfo 생성자.
        /// </summary>
        /// <param name="userId">플레이어의 사용자 ID</param>
        public PlayerSessionInfo(string userId)
        {
            _userId = userId;
            _userName = string.Empty;
            _currentState = SessionState.Authenticating;
            _matchId = -1;
        }

        #endregion
    }
}
