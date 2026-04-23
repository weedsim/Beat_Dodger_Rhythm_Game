using System.Collections.Generic;
using Mirror;
using BeatDodger.Core;

namespace BeatDodger.Sessions
{
    /// <summary>
    /// 인게임 플레이어 4인을 격리하여 관리하는 논리적 매치 세션
    /// </summary>
    public class MatchSession
    {
        #region Variables

        private int _matchId;
        private string _matchName;
        private List<NetworkConnectionToClient> _participants = new List<NetworkConnectionToClient>();
        private SessionState _matchState;

        #endregion

        #region Properties

        public int MatchId
        {
            get
            {
               return _matchId;
            }
        }
        public string MatchName
        {
            get
            {
                return _matchName;
            }
        }

        public SessionState MatchState
        {
            get
            {
                return _matchState;
            }
            set
            {
                _matchState = value;
            }
        }

        public IReadOnlyList<NetworkConnectionToClient> Participants
        {
            get
            {
                return _participants;
            }
        }

        #endregion

        #region Constructor

        public MatchSession(int matchId, string matchName)
        {
            _matchId = matchId;
            _matchName = matchName;
            _matchState = SessionState.Lobby;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 세션에 플레이어를 추가한다.
        /// </summary>
        /// <param name="conn">추가할 플레이어의 네트워크 연결</param>
        /// <returns>추가 성공 여부</returns>
        public bool AddPlayer(NetworkConnectionToClient conn)
        {
            if (_participants.Count >= 4) return false;

            if (!_participants.Contains(conn))
            {
                _participants.Add(conn);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 세션에서 플레이어를 제거한다.
        /// </summary>
        /// <param name="conn">제거할 플레이어의 네트워크 연결</param>
        public void RemovePlayer(NetworkConnectionToClient conn)
        {
            if (_participants.Contains(conn))
            {
                _participants.Remove(conn);
            }
        }

        #endregion
    }
}