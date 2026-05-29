using System;
using System.Collections.Generic;
using BeatDodger.Core;
using BeatDodger.Managers;
using BeatDodger.Multiplayer;
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
        public event Action OnJoinFailed;

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
        /// <param name="password">방 비밀번호. 빈 문자열이면 공개방으로 생성된다.</param>
        /// <param name="songId">방장이 선택한 곡 고유 ID</param>
        /// <param name="songName">방장이 선택한 곡 이름</param>
        public void RequestCreateParty(string roomName, string password, int songId, string songName)
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
            CmdCreateParty(roomName, password, songId, songName);
        }

        /// <summary>
        /// 특정 파티에 참가 요청을 서버로 전송한다. (클라이언트 호출)
        /// </summary>
        /// <param name="partyId">참가할 파티의 고유 ID</param>
        /// <param name="password">입력한 비밀번호. 공개방이면 빈 문자열을 전달한다.</param>
        public void RequestJoinParty(int partyId, string password)
        {
            if (NetworkClient.connection == null)
            {
                return;
            }

            if (NetworkClient.connection.identity == null)
            {
                return;
            }

            CmdJoinParty(partyId, password);
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
        /// 방장이 대기실에서 선택한 곡 정보를 서버로 전송한다. (클라이언트 호출)
        /// 5초 이상 같은 곡을 선택 상태로 유지했을 때만 RoomUIController가 호출한다.
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="songId">선택한 곡 고유 ID</param>
        /// <param name="songName">선택한 곡 이름</param>
        /// <param name="difficulty">선택한 곡 난이도</param>
        /// <param name="tags">선택한 곡 해시태그 분위기 문자열</param>
        public void RequestUpdateSong(int partyId, int songId, string songName, int difficulty, string tags)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Client] RequestUpdateSong: connection 또는 identity 가 없습니다.");
                return;
            }

            CmdUpdateSong(partyId, songId, songName, difficulty, tags);
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

            PartyInfo leaderParty = SnapshotLeaderParty(disconnectedNetId);

            List<PartyInfo> updatedParties = _service.HandlePlayerDisconnect(disconnectedNetId);

            // SyncList를 갱신하여 변경 사항을 클라이언트에 동기화
            SyncUpdatedParties(updatedParties);

            // 파티 비활성화 후 나머지 멤버를 강제 퇴실한다 (클라이언트가 비활성 파티 목록을 먼저 수신)
            if (leaderParty._PartyId != 0)
            {
                KickPartyMembers(leaderParty);
            }
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
        private void CmdCreateParty(string roomName, string password, int songId, string songName, NetworkConnectionToClient sender = null)
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

            if (string.IsNullOrWhiteSpace(roomName) || roomName.Length > PartyConstants.MAX_ROOM_NAME_LENGTH)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdCreateParty: 방 이름 길이 위반 | len={roomName?.Length}");
                return;
            }

            if (password != null && password.Length > PartyConstants.MAX_PASSWORD_LENGTH)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdCreateParty: 비밀번호 길이 위반");
                return;
            }

            if (songName != null && songName.Length > PartyConstants.MAX_SONG_NAME_LENGTH)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdCreateParty: 곡 이름 길이 위반");
                return;
            }

            uint leaderNetId = sender.identity.netId;
            PartyInfo? created = _service.CreateParty(roomName, password, songId, songName ?? string.Empty, leaderNetId);

            if (created.HasValue)
            {
                _partyList.Add(created.Value);

                if (sender.identity.TryGetComponent(out LobbyPlayer lobbyPlayer))
                {
                    lobbyPlayer.SetCurrentPartyId(created.Value._PartyId);
                }
            }
            else
            {
                Debug.Log("[PartyNetworkBridge] [Server] CmdCreateParty: 방이 최종 생성되지 않았습니다");
            }
        }

        [Command(requiresAuthority = false)]
        private void CmdJoinParty(int targetPartyId, string password, NetworkConnectionToClient sender = null)
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

            if (password != null && password.Length > PartyConstants.MAX_PASSWORD_LENGTH)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdJoinParty: 비밀번호 길이 위반");
                return;
            }

            uint joinerNetId = sender.identity.netId;
            PartyInfo? updated = _service.JoinParty(targetPartyId, password, joinerNetId);

            if (!updated.HasValue)
            {
                TargetNotifyJoinFailed(sender);
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

            // 모든 플레이어가 악기를 선택(=준비 완료)하면 자동으로 매치를 시작한다
            if (_service.CanStartMatch(partyId, out PartyInfo readyParty))
            {
                Debug.Log($"[PartyNetworkBridge] [Server] 전원 악기 선택 완료 — 자동 매치 시작 | PartyId: {partyId}");
                StartMatchServerSide(readyParty);
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
            PartyInfo leaderParty = SnapshotLeaderParty(netId);
            List<PartyInfo> updatedParties = _service.HandlePlayerDisconnect(netId);
            SyncUpdatedParties(updatedParties);
            lobbyPlayer.SetCurrentPartyId(0);

            // 파티 비활성화 후 나머지 멤버를 강제 퇴실한다 (클라이언트가 비활성 파티 목록을 먼저 수신)
            if (leaderParty._PartyId != 0)
            {
                KickPartyMembers(leaderParty);
            }
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
        private void CmdUpdateSong(int partyId, int songId, string songName, int difficulty, string tags, NetworkConnectionToClient sender = null)
        {
            if (_service == null)
            {
                return;
            }

            if (sender == null || sender.identity == null)
            {
                Debug.LogWarning("[PartyNetworkBridge] [Server] CmdUpdateSong: sender 또는 identity 가 없습니다.");
                return;
            }

            if (songName != null && songName.Length > PartyConstants.MAX_SONG_NAME_LENGTH)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdUpdateSong: 곡 이름 길이 위반");
                return;
            }

            if (tags != null && tags.Length > PartyConstants.MAX_SONG_TAGS_LENGTH)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] CmdUpdateSong: 곡 태그 길이 위반");
                return;
            }

            uint netId = sender.identity.netId;
            PartyInfo? updated = _service.UpdateSong(partyId, songId, songName ?? string.Empty, difficulty, tags ?? string.Empty, netId);

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

            // 재진입 방지: SyncList 상 이미 비활성화된 파티는 무시 (CmdSelectInstrument 중복 호출 대비)
            bool alreadyInactive = true;
            for (int check = 0; check < _partyList.Count; check++)
            {
                if (_partyList[check]._PartyId == party._PartyId)
                {
                    alreadyInactive = !_partyList[check]._IsActive;
                    break;
                }
            }

            if (alreadyInactive)
            {
                Debug.LogWarning($"[PartyNetworkBridge] [Server] StartMatchServerSide: 이미 비활성화된 파티 | PartyId: {party._PartyId}");
                return;
            }

            // 매치 시작 전 즉시 비활성화하여 후속 Cmd의 중복 시작 차단
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

            // 서버 저장소의 비밀번호 항목 즉시 정리
            _repository.RemovePasswordEntry(party._PartyId);

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

            // 4명 전원에게 EnterGameMessage 전송 (UI 패널 전환용)
            EnterGameMessage enterMsg = new EnterGameMessage
            {
                MatchId = matchId,
                Difficulty = party._Difficulty,
                SongId = party._SongId,
                SongName = party._SongName
            };
            _sessionCoordinator.SendToMatch(matchId, enterMsg);

            // 인게임 노트 스케줄 시작 — GameNetworkBridge가 GameStartMessage와 NoteSpawnMessage를 파티에 전송한다.
            // bpm과 beatsToArrive는 곡 데이터에서 조회 필요; 현재는 기본값 사용.
            GameNetworkBridge.Instance?.StartMatch(
                matchId, party._SongId, party._SongName, party._Difficulty,
                bpm: 120f, beatsToArrive: 4);
        }

        [TargetRpc]
        private void TargetNotifyJoinFailed(NetworkConnectionToClient target)
        {
            OnJoinFailed?.Invoke();
        }

        // 서비스 호출로 파티가 비활성화되기 전에 멤버 스냅샷을 캡처한다
        [Server]
        private PartyInfo SnapshotLeaderParty(uint leaderNetId)
        {
            for (int i = 0; i < _partyList.Count; i++)
            {
                PartyInfo p = _partyList[i];
                if (p._IsActive && p._Slot0NetId == leaderNetId)
                {
                    return p;
                }
            }
            return default;
        }

        // SyncList 갱신(비활성화) 후에 멤버들의 파티 ID를 초기화한다
        [Server]
        private void KickPartyMembers(PartyInfo leaderParty)
        {
            for (int slot = 1; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                uint memberNetId = leaderParty.GetSlot(slot);
                if (memberNetId == 0)
                {
                    continue;
                }

                if (NetworkServer.spawned.TryGetValue(memberNetId, out NetworkIdentity memberIdentity) &&
                    memberIdentity != null &&
                    memberIdentity.TryGetComponent(out LobbyPlayer memberPlayer))
                {
                    memberPlayer.SetCurrentPartyId(0);
                    Debug.Log($"[PartyNetworkBridge] [Server] 방장 퇴장 — 멤버 강제 퇴실 | MemberNetId: {memberNetId} | PartyId: {leaderParty._PartyId}");
                }
            }
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
