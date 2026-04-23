using System;
using System.Collections.Generic;
using BeatDodger.Core;
using BeatDodger.Managers;
using BeatDodger.Network;
using Mirror;
using UnityEngine;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 파티 시스템의 네트워크 계층 브릿지.
    /// SyncList 동기화, [Command] 수신, 클라이언트 요청 API를 담당하며
    /// 비즈니스 로직은 PartyService에 위임한다.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class PartyNetworkBridge : NetworkBehaviour
    {
        #region Variables

        private static PartyNetworkBridge _instance;

        [Header("Party Settings")]
        [SerializeField, Range(1, 50), Tooltip("로비에서 허용하는 최대 파티 수")]
        private int _maxParties = PartyConstants.DEFAULT_MAX_PARTIES;

        [Header("Server References")]
        [SerializeField, Tooltip("세션 코디네이터 (서버 전용, Inspector에서 연결)")]
        private SessionCoordinator _sessionCoordinator;

        private readonly SyncList<PartyInfo> _partyList = new SyncList<PartyInfo>();

        private PartyRepository _repository;
        private PartyService _service;

        public event Action OnPartyListUpdated;

        #endregion

        #region Properties

        /// <summary>
        /// PartyNetworkBridge 싱글톤 인스턴스.
        /// </summary>
        public static PartyNetworkBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<PartyNetworkBridge>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 로비에서 허용하는 최대 파티 수.
        /// </summary>
        public int MaxParties
        {
            get
            {
                return _maxParties;
            }
        }

        /// <summary>
        /// 클라이언트에 동기화되는 파티 목록 (읽기 전용).
        /// </summary>
        public SyncList<PartyInfo> PartyList
        {
            get
            {
                return _partyList;
            }
        }

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                if (gameObject != null)
                {
                    Destroy(gameObject);
                }
                return;
            }

            // 서버 빌드인 경우 Awake 시점에 미리 초기화 (OnStartServer 전에 컴포넌트 참조가 필요한 경우 대비)
#if UNITY_SERVER
            InitializeServerSystems();
#endif
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Awake 이후 NetworkServer.active가 확정된 시점에서도 초기화 보장
            if (_repository == null)
            {
                InitializeServerSystems();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            _partyList.Callback += HandlePartyListChanged;
            OnPartyListUpdated?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            _partyList.Callback -= HandlePartyListChanged;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 새로운 파티 생성 요청을 서버로 전송한다. (클라이언트 호출)
        /// </summary>
        /// <param name="roomName">생성할 방 이름</param>
        /// <param name="difficulty">선택한 난이도</param>
        public void RequestCreateParty(string roomName, int difficulty)
        {
            if (NetworkClient.connection == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] NetworkClient.connection 이 없습니다.");
                return;
            }

            if (NetworkClient.connection.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] NetworkClient.connection.identity 가 없습니다.");
                return;
            }

            Debug.Log("[PartyNetworkBridge] [Client] 파티 생성 요청을 서버에 전송합니다.");
            CmdCreateParty(roomName, difficulty);
        }

        /// <summary>
        /// 특정 파티에 참가 요청을 서버로 전송한다. (클라이언트 호출)
        /// </summary>
        /// <param name="partyId">참가할 파티의 고유 ID</param>
        public void RequestJoinParty(int partyId)
        {
            if (NetworkClient.connection == null)
            {
                return;
            }

            if (NetworkClient.connection.identity == null)
            {
                return;
            }

            CmdJoinParty(partyId);
        }

        /// <summary>
        /// 활성화된 파티 목록을 가져와 매개변수로 전달된 리스트에 채운다.
        /// </summary>
        /// <param name="outParties">결과를 담을 미리 할당된 리스트 (Zero-GC 패턴)</param>
        public void GetActiveParties(List<PartyInfo> outParties)
        {
            outParties.Clear();
            int count = _partyList.Count;

            for (int i = 0; i < count; i++)
            {
                if (_partyList[i]._IsActive)
                {
                    outParties.Add(_partyList[i]);
                }
            }
        }

        /// <summary>
        /// 악기 선택 요청을 서버로 전송한다. (클라이언트 호출)
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="instrument">선택할 악기 종류</param>
        public void RequestSelectInstrument(int partyId, InstrumentType instrument)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] RequestSelectInstrument: connection 또는 identity 가 없습니다.");
                return;
            }

            CmdSelectInstrument(partyId, instrument);
        }

        /// <summary>
        /// 현재 속한 파티에서 나가기 요청을 서버로 전송한다. (클라이언트 호출)
        /// </summary>
        public void RequestLeaveParty()
        {
            if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] RequestLeaveParty: connection 또는 identity 가 없습니다.");
                return;
            }

            CmdLeaveParty();
        }

        /// <summary>
        /// 본인의 준비 상태 변경 요청을 서버로 전송한다. (클라이언트 호출)
        /// </summary>
        /// <param name="ready">설정할 준비 상태</param>
        public void RequestSetReady(bool ready)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] RequestSetReady: connection 또는 identity 가 없습니다.");
                return;
            }

            CmdSetReady(ready);
        }

        /// <summary>
        /// 게임 시작 요청을 서버로 전송한다. 방장만 호출할 수 있다. (클라이언트 호출)
        /// </summary>
        public void RequestStartMatch()
        {
            if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] RequestStartMatch: connection 또는 identity 가 없습니다.");
                return;
            }

            CmdRequestStartMatch();
        }

        /// <summary>
        /// 플레이어 접속 해제 시 해당 플레이어가 속한 파티 데이터를 정리한다. (서버 전용)
        /// </summary>
        /// <param name="disconnectedNetId">접속이 끊긴 플레이어의 고유 NetId</param>
        [Server]
        public void HandlePlayerDisconnect(uint disconnectedNetId)
        {
            if (_service == null)
            {
                return;
            }

            List<PartyInfo> updatedParties = _service.HandlePlayerDisconnect(disconnectedNetId);

            // SyncList를 갱신하여 변경 사항을 클라이언트에 동기화
            SyncUpdatedParties(updatedParties);
        }

        #endregion

        #region Private Methods

        private void InitializeServerSystems()
        {
            _repository = new PartyRepository();
            _service = new PartyService(_repository, _maxParties);

            if (_sessionCoordinator == null)
            {
                _sessionCoordinator = FindAnyObjectByType<SessionCoordinator>();
            }
        }

        private void HandlePartyListChanged(SyncList<PartyInfo>.Operation op, int index, PartyInfo oldItem, PartyInfo newItem)
        {
            OnPartyListUpdated?.Invoke();
        }

        [Command(requiresAuthority = false)]
        private void CmdCreateParty(string roomName, int difficulty, NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdCreateParty: sender 또는 identity 가 없습니다.");
                return;
            }

            uint leaderNetId = sender.identity.netId;
            PartyInfo? created = _service.CreateParty(roomName, difficulty, leaderNetId);

            if (created.HasValue)
            {
                _partyList.Add(created.Value);

                if (sender.identity.TryGetComponent(out LobbyPlayer lobbyPlayer))
                {
                    lobbyPlayer.SetCurrentPartyId(created.Value._PartyId);
                }
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdJoinParty(int targetPartyId, NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdJoinParty: sender 또는 identity 가 없습니다.");
                return;
            }

            uint joinerNetId = sender.identity.netId;
            PartyInfo? updated = _service.JoinParty(targetPartyId, joinerNetId);

            if (!updated.HasValue)
            {
                return;
            }

            // SyncList에서 해당 파티 인덱스를 찾아 갱신
            for (int i = 0; i < _partyList.Count; i++)
            {
                if (_partyList[i]._PartyId == targetPartyId)
                {
                    _partyList[i] = updated.Value;
                    break;
                }
            }

            if (sender.identity.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                lobbyPlayer.SetCurrentPartyId(targetPartyId);
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdSelectInstrument(int partyId, InstrumentType instrument, NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdSelectInstrument: sender 또는 identity 가 없습니다.");
                return;
            }

            uint netId = sender.identity.netId;
            PartyInfo? updated = _service.SelectInstrument(partyId, netId, instrument);

            if (!updated.HasValue)
            {
                return;
            }

            for (int i = 0; i < _partyList.Count; i++)
            {
                if (_partyList[i]._PartyId == partyId)
                {
                    _partyList[i] = updated.Value;
                    break;
                }
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdLeaveParty(NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdLeaveParty: sender 또는 identity 가 없습니다.");
                return;
            }

            if (!sender.identity.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                return;
            }

            uint netId = sender.identity.netId;
            List<PartyInfo> updatedParties = _service.HandlePlayerDisconnect(netId);
            SyncUpdatedParties(updatedParties);
            lobbyPlayer.SetCurrentPartyId(0);
        }

        [Command(requiresAuthority = false)]
        private void CmdSetReady(bool ready, NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdSetReady: sender 또는 identity 가 없습니다.");
                return;
            }

            if (!sender.identity.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                return;
            }

            int partyId = lobbyPlayer.CurrentPartyId;
            if (partyId == 0)
            {
                return;
            }

            uint netId = sender.identity.netId;
            PartyInfo? updated = _service.SetReady(partyId, netId, ready);

            if (!updated.HasValue)
            {
                return;
            }

            for (int i = 0; i < _partyList.Count; i++)
            {
                if (_partyList[i]._PartyId == partyId)
                {
                    _partyList[i] = updated.Value;
                    break;
                }
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestStartMatch(NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdRequestStartMatch: sender 또는 identity 가 없습니다.");
                return;
            }

            if (!sender.identity.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                return;
            }

            int partyId = lobbyPlayer.CurrentPartyId;
            if (partyId == 0)
            {
                return;
            }

            // 파티 찾기
            PartyInfo targetParty = default;
            bool found = false;
            for (int i = 0; i < _partyList.Count; i++)
            {
                if (_partyList[i]._PartyId == partyId)
                {
                    targetParty = _partyList[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdRequestStartMatch: partyId {partyId} 를 찾을 수 없습니다.");
                return;
            }

            // 방장 검증
            if (sender.identity.netId != targetParty._Slot0NetId)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdRequestStartMatch: 방장이 아닌 플레이어의 요청 | NetId: {sender.identity.netId}");
                return;
            }

            // 시작 조건 검증
            if (!_service.CanStartMatch(partyId, out PartyInfo validatedParty))
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdRequestStartMatch: 시작 조건 미충족 | PartyId: {partyId}");
                return;
            }

            StartMatchServerSide(validatedParty);
        }

        private void StartMatchServerSide(PartyInfo party)
        {
            if (_sessionCoordinator == null)
            {
                Debug.LogError("[PartyNetworkBridge] [Server] StartMatchServerSide: SessionCoordinator 참조가 없습니다.");
                return;
            }

            // 슬롯 netId → NetworkConnectionToClient 변환
            List<NetworkConnectionToClient> participants = new List<NetworkConnectionToClient>();
            for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                uint slotNetId = party.GetSlot(slot);
                if (slotNetId == 0)
                {
                    continue;
                }

                NetworkConnectionToClient conn = _sessionCoordinator.GetConnectionByNetId(slotNetId);
                if (conn != null)
                {
                    participants.Add(conn);
                }
                else if (NetworkServer.spawned.TryGetValue(slotNetId, out NetworkIdentity identity) && identity != null)
                {
                    if (identity.connectionToClient != null)
                    {
                        participants.Add(identity.connectionToClient);
                    }
                }
            }

            int matchId = _sessionCoordinator.CreateMatch(participants, party._RoomName);

            Debug.Log($"[PartyNetworkBridge] [Server] 매치 생성 완료 | MatchId: {matchId} | PartyId: {party._PartyId} | 방 이름: {party._RoomName}");

            // 파티 비활성화 (인게임 전환 후 방은 비활성 상태로 전환)
            for (int i = 0; i < _partyList.Count; i++)
            {
                if (_partyList[i]._PartyId == party._PartyId)
                {
                    PartyInfo deactivated = _partyList[i];
                    deactivated._IsActive = false;
                    _partyList[i] = deactivated;
                    break;
                }
            }

            // 4명 전원에게 EnterGameMessage 전송
            EnterGameMessage enterMsg = new EnterGameMessage
            {
                MatchId = matchId,
                Difficulty = party._Difficulty
            };
            _sessionCoordinator.SendToMatch(matchId, enterMsg);
        }

        private void SyncUpdatedParties(List<PartyInfo> updatedParties)
        {
            for (int u = 0; u < updatedParties.Count; u++)
            {
                int partyId = updatedParties[u]._PartyId;

                for (int i = 0; i < _partyList.Count; i++)
                {
                    if (_partyList[i]._PartyId == partyId)
                    {
                        _partyList[i] = updatedParties[u];
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
