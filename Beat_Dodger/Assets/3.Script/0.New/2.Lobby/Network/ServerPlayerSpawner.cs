using Mirror;
using UnityEngine;
using BeatDodger.Lobby;
using BeatDodger.Managers;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버 사이드 플레이어 스폰 전담 클래스.
    /// LobbyPlayer 생성, 세션 NetId 등록을 책임진다.
    /// </summary>
    public class ServerPlayerSpawner : IPlayerSpawner
    {
        private readonly GameObject _playerPrefab;
        private readonly SessionCoordinator _sessionCoordinator;

        public ServerPlayerSpawner(GameObject playerPrefab, SessionCoordinator sessionCoordinator)
        {
            _playerPrefab = playerPrefab;
            _sessionCoordinator = sessionCoordinator;
        }

        public void SpawnPlayer(NetworkConnectionToClient conn)
        {
            GameObject playerGo = Object.Instantiate(_playerPrefab);
            NetworkServer.AddPlayerForConnection(conn, playerGo);

            if (playerGo.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                string userId = _sessionCoordinator.GetUserIdByConnection(conn);
                string userName = _sessionCoordinator.GetUserNameByConnection(conn);
                lobbyPlayer.SetIdentity(userId, userName);
            }

            _sessionCoordinator.SetPlayerNetId(conn, conn.identity.netId);

            Debug.Log($"[ServerPlayerSpawner] LobbyPlayer spawned for conn {conn.connectionId} | netId: {conn.identity.netId}");
        }
    }
}
