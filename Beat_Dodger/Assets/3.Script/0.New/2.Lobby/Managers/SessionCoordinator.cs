using System.Collections.Generic;
using Mirror;
using UnityEngine;
using BeatDodger.Core;
using BeatDodger.Sessions;

namespace BeatDodger.Managers
{
    /// <summary>
    /// 서버 내의 모든 플레이어 세션과 매치 세션을 총괄 관리하는 컨트롤러
    /// </summary>
    public class SessionCoordinator : MonoBehaviour
    {
        #region Variables

        private Dictionary<NetworkConnectionToClient, PlayerSessionInfo> _playerSessions = new Dictionary<NetworkConnectionToClient, PlayerSessionInfo>();
        private Dictionary<int, MatchSession> _activeMatches = new Dictionary<int, MatchSession>();
        private Dictionary<uint, NetworkConnectionToClient> _netIdToConnection = new Dictionary<uint, NetworkConnectionToClient>();
        private int _nextMatchId = 1000;

        #endregion

        #region Public Methods

        /// <summary>
        /// 새로운 플레이어 접속 시 세션 정보를 등록한다.
        /// </summary>
        /// <param name="conn">플레이어의 네트워크 연결</param>
        /// <param name="userId">사용자 고유 ID</param>
        public void RegisterPlayer(NetworkConnectionToClient conn, string userId)
        {
            if (_playerSessions.ContainsKey(conn))
            {
                return;
            }

            _playerSessions.Add(conn, new PlayerSessionInfo(userId));
        }

        /// <summary>
        /// 플레이어 접속 종료 시 세션 정보를 제거하고 소속된 매치에서 탈퇴시킨다.
        /// </summary>
        /// <param name="conn">플레이어의 네트워크 연결</param>
        public void UnregisterPlayer(NetworkConnectionToClient conn)
        {
            if (!_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return;
            }

            if (session.MatchId != -1)
            {
                RemovePlayerFromMatch(conn);
            }

            if (session.NetId != 0)
            {
                _netIdToConnection.Remove(session.NetId);
            }

            _playerSessions.Remove(conn);
        }

        /// <summary>
        /// 플레이어의 세션 상태를 변경한다.
        /// </summary>
        /// <param name="conn">플레이어의 네트워크 연결</param>
        /// <param name="newState">변경할 세션 상태</param>
        public void UpdatePlayerState(NetworkConnectionToClient conn, SessionState newState)
        {
            if (_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                session.CurrentState = newState;
            }
        }

        /// <summary>
        /// 로그인 성공 시 DB에서 로드된 실제 사용자 정보(UserId, UserName)로 세션을 갱신한다.
        /// 접속 시 등록된 임시 userId 를 덮어쓴다.
        /// </summary>
        /// <param name="conn">플레이어의 네트워크 연결</param>
        /// <param name="userId">DB에서 조회된 실제 사용자 ID</param>
        /// <param name="userName">DB에서 조회된 표시용 사용자 이름</param>
        public void UpdatePlayerUserInfo(NetworkConnectionToClient conn, string userId, string userName)
        {
            if (!_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return;
            }

            session.UserId = userId;
            session.UserName = userName;
        }

        /// <summary>
        /// 특정 연결의 PlayerSessionInfo 전체를 반환한다.
        /// </summary>
        /// <param name="conn">조회할 플레이어의 네트워크 연결</param>
        /// <returns>PlayerSessionInfo. 세션이 없으면 null.</returns>
        public PlayerSessionInfo GetPlayerSession(NetworkConnectionToClient conn)
        {
            if (_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return session;
            }

            return null;
        }

        /// <summary>
        /// 특정 연결의 현재 세션 상태를 반환한다.
        /// </summary>
        /// <param name="conn">조회할 플레이어의 네트워크 연결</param>
        /// <returns>현재 SessionState. 세션이 없으면 Authenticating.</returns>
        public SessionState GetPlayerState(NetworkConnectionToClient conn)
        {
            if (_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return session.CurrentState;
            }

            return SessionState.Authenticating;
        }

        /// <summary>
        /// 특정 연결의 NetId를 반환한다. identity가 없으면 0을 반환한다.
        /// </summary>
        /// <param name="conn">조회할 플레이어의 네트워크 연결</param>
        /// <returns>NetId 값. identity 미존재 시 0.</returns>
        public uint GetNetId(NetworkConnectionToClient conn)
        {
            if (conn != null && conn.identity != null)
            {
                return conn.identity.netId;
            }

            return 0;
        }

        /// <summary>
        /// 스폰된 LobbyPlayer 의 netId 를 세션에 저장하고 역방향 캐시를 갱신한다.
        /// </summary>
        /// <param name="conn">대상 플레이어의 네트워크 연결</param>
        /// <param name="netId">스폰된 NetworkBehaviour 의 netId</param>
        public void SetPlayerNetId(NetworkConnectionToClient conn, uint netId)
        {
            if (!_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return;
            }

            session.NetId = netId;
            _netIdToConnection[netId] = conn;
        }

        /// <summary>
        /// netId 로 연결을 역방향 조회한다.
        /// </summary>
        /// <param name="netId">조회할 netId</param>
        /// <returns>매핑된 NetworkConnectionToClient. 없으면 null.</returns>
        public NetworkConnectionToClient GetConnectionByNetId(uint netId)
        {
            if (_netIdToConnection.TryGetValue(netId, out NetworkConnectionToClient conn))
            {
                return conn;
            }

            return null;
        }

        /// <summary>
        /// 연결로부터 저장된 netId 를 반환한다.
        /// </summary>
        /// <param name="conn">조회할 플레이어의 네트워크 연결</param>
        /// <returns>저장된 netId. 세션이 없거나 미설정 시 0.</returns>
        public uint GetNetIdByConnection(NetworkConnectionToClient conn)
        {
            if (_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return session.NetId;
            }

            return 0;
        }

        /// <summary>
        /// 연결로부터 UserId를 반환한다.
        /// </summary>
        public string GetUserIdByConnection(NetworkConnectionToClient conn)
        {
            return _playerSessions.TryGetValue(conn, out PlayerSessionInfo session)
                ? session.UserId
                : string.Empty;
        }

        /// <summary>
        /// 연결로부터 UserName을 반환한다.
        /// </summary>
        public string GetUserNameByConnection(NetworkConnectionToClient conn)
        {
            return _playerSessions.TryGetValue(conn, out PlayerSessionInfo session)
                ? session.UserName
                : string.Empty;
        }

        /// <summary>
        /// 특정 플레이어들을 모아 새로운 인게임 매치를 생성한다.
        /// </summary>
        /// <param name="players">매치에 참여시킬 플레이어 리스트</param>
        /// <param name="matchName">생성할 방 이름</param>
        /// <returns>생성된 매치의 고유 ID</returns>
        public int CreateMatch(List<NetworkConnectionToClient> players, string matchName)
        {
            int matchId = _nextMatchId++;
            MatchSession newMatch = new MatchSession(matchId, matchName);

            foreach (NetworkConnectionToClient conn in players)
            {
                if (newMatch.AddPlayer(conn))
                {
                    UpdatePlayerMatchInfo(conn, matchId, SessionState.InGame);
                }
            }

            _activeMatches.Add(matchId, newMatch);
            return matchId;
        }

        /// <summary>
        /// 특정 매치에 참가 중인 연결 목록을 반환한다. GameNetworkBridge에서 슬롯 순서 접근에 사용.
        /// </summary>
        /// <param name="matchId">조회할 매치 ID</param>
        /// <returns>참가자 연결 목록. 매치가 없으면 null.</returns>
        public IReadOnlyList<NetworkConnectionToClient> GetMatchParticipants(int matchId)
        {
            if (_activeMatches.TryGetValue(matchId, out MatchSession match))
                return match.Participants;
            return null;
        }

        /// <summary>
        /// 특정 매치에 소속된 모든 플레이어에게만 메시지를 전송한다. (논리적 격리 핵심)
        /// </summary>
        /// <typeparam name="T">전송할 메시지의 구체적인 타입</typeparam>
        /// <param name="matchId">대상 매치 ID</param>
        /// <param name="message">전송할 네트워크 메시지 객체</param>
        public void SendToMatch<T>(int matchId, T message) where T : struct, NetworkMessage
        {
            if (!_activeMatches.TryGetValue(matchId, out MatchSession match))
            {
                return;
            }

            foreach (NetworkConnectionToClient conn in match.Participants)
            {
                conn.Send(message);
            }
        }

        #endregion

        #region Private Methods

        private void UpdatePlayerMatchInfo(NetworkConnectionToClient conn, int matchId, SessionState state)
        {
            if (_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                session.MatchId = matchId;
                session.CurrentState = state;
            }
        }

        private void RemovePlayerFromMatch(NetworkConnectionToClient conn)
        {
            if (!_playerSessions.TryGetValue(conn, out PlayerSessionInfo session))
            {
                return;
            }

            if (_activeMatches.TryGetValue(session.MatchId, out MatchSession match))
            {
                match.RemovePlayer(conn);

                if (match.Participants.Count == 0)
                {
                    _activeMatches.Remove(session.MatchId);
                }
            }
        }

        #endregion
    }
}
