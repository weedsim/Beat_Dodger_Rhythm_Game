using UnityEngine;
using Mirror;
using BeatDodger.Network;
using BeatDodger.UI;

namespace BeatDodger.Multiplayer
{
    /// <summary>
    /// 인게임 진입 후 클라이언트가 보유하는 매치 컨텍스트.
    /// EnterGameMessage 또는 GameStartMessage 수신 시 Initialize()를 호출한다.
    /// DontDestroyOnLoad이므로 씬 전환(UI 패널 전환) 사이에도 유지된다.
    /// </summary>
    public class GameSessionContext : MonoBehaviour
    {
        public static GameSessionContext Instance { get; private set; }

        /// <summary>현재 매치 ID. 미진입 상태이면 -1.</summary>
        public int MatchId { get; private set; } = -1;

        /// <summary>이 클라이언트의 슬롯 인덱스 (0~3).</summary>
        public int SlotIndex { get; private set; } = -1;

        /// <summary>
        /// 멀티플레이어 모드인지 여부.
        /// [수정] auto-property → SerializeField 백킹 필드 + 읽기 전용 프로퍼티로 변경.
        ///        Inspector에서 미리 true로 설정하면 NewRhythmManager.Start()보다
        ///        GameSessionContext.Awake()가 먼저 실행되므로 타이밍 문제 없이 가드가 동작한다.
        ///        Initialize()에서도 여전히 true로 덮어쓰므로 런타임 동작은 동일.
        /// </summary>
        [SerializeField] private bool _isMultiplayerMode;
        public bool IsMultiplayerMode => _isMultiplayerMode;

        /// <summary>서버로부터 받은 게임 시작 DSP 기준 시간.</summary>
        public double ServerStartDspTime { get; private set; }

        /// <summary>이 클라이언트의 GamePlayer NetworkBehaviour. 스폰 후 자동 등록.</summary>
        public GamePlayer LocalGamePlayer { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                    transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            NetworkClient.RegisterHandler<EnterGameMessage>(OnEnterGameReceived);
            NetworkClient.RegisterHandler<NoteSpawnMessage>(OnNoteSpawnReceived);
            NetworkClient.RegisterHandler<NoteHitResultMessage>(OnNoteHitResultReceived);
            NetworkClient.RegisterHandler<GameStartMessage>(OnGameStartReceived);
            NetworkClient.RegisterHandler<GameEndMessage>(OnGameEndReceived);
            // [추가] 피버/보스전 메시지 핸들러
            NetworkClient.RegisterHandler<FeverStartMessage>(OnFeverStartReceived);
            NetworkClient.RegisterHandler<FeverUpdateMessage>(OnFeverUpdateReceived);
            NetworkClient.RegisterHandler<FeverResultMessage>(OnFeverResultReceived);
        }

        private void OnDisable()
        {
            NetworkClient.UnregisterHandler<EnterGameMessage>();
            NetworkClient.UnregisterHandler<NoteSpawnMessage>();
            NetworkClient.UnregisterHandler<NoteHitResultMessage>();
            NetworkClient.UnregisterHandler<GameStartMessage>();
            NetworkClient.UnregisterHandler<GameEndMessage>();
            // [추가] 피버/보스전 메시지 핸들러
            NetworkClient.UnregisterHandler<FeverStartMessage>();
            NetworkClient.UnregisterHandler<FeverUpdateMessage>();
            NetworkClient.UnregisterHandler<FeverResultMessage>();
        }

        /// <summary>
        /// 매치 진입 시 초기화. PartyNetworkBridge → EnterGameMessage 수신 후 호출.
        /// </summary>
        public void Initialize(int matchId, int slotIndex)
        {
            MatchId = matchId;
            SlotIndex = slotIndex;
            _isMultiplayerMode = true;
            Debug.Log($"[GameSessionContext] Initialized — MatchId: {matchId}, Slot: {slotIndex}");
        }

        /// <summary>
        /// 로컬 GamePlayer가 스폰된 후 스스로 등록한다.
        /// </summary>
        public void RegisterLocalGamePlayer(GamePlayer player)
        {
            LocalGamePlayer = player;
        }

        /// <summary>
        /// 게임 종료 또는 로비 복귀 시 컨텍스트를 초기화한다.
        /// </summary>
        public void Reset()
        {
            MatchId = -1;
            SlotIndex = -1;
            _isMultiplayerMode = false;
            ServerStartDspTime = 0;
            LocalGamePlayer = null;
        }

        // ---------------------------------------------------------------
        // Message Handlers — MultiplayerRhythmManager에 위임
        // ---------------------------------------------------------------

        private void OnEnterGameReceived(EnterGameMessage msg)
        {
            // slotIndex는 이후 GameStartMessage에서 확정되므로 임시로 -1 설정
            Initialize(msg.MatchId, -1);
            UIManager.Instance?.TransitionToInGame();
            Debug.Log($"[GameSessionContext] 인게임 진입 — MatchId: {msg.MatchId}, Song: {msg.SongName}");
        }

        private void OnGameStartReceived(GameStartMessage msg)
        {
            if (msg.MatchId != MatchId)
            {
                // 이 클라이언트가 속하지 않은 매치 메시지는 무시
                // (실제로는 SessionCoordinator.SendToMatch()가 격리하므로 도달하지 않음)
                return;
            }

            ServerStartDspTime = msg.ServerStartDspTime;
            SlotIndex = msg.SlotIndex;

            MultiplayerRhythmManager.Instance?.OnGameStartReceived(msg);
        }

        private void OnNoteSpawnReceived(NoteSpawnMessage msg)
        {
            if (msg.MatchId != MatchId) return;
            MultiplayerRhythmManager.Instance?.OnNoteSpawnReceived(msg);
        }

        private void OnNoteHitResultReceived(NoteHitResultMessage msg)
        {
            if (msg.MatchId != MatchId) return;
            MultiplayerRhythmManager.Instance?.OnNoteHitResultReceived(msg);
        }

        private void OnGameEndReceived(GameEndMessage msg)
        {
            if (msg.MatchId != MatchId) return;
            MultiplayerRhythmManager.Instance?.OnGameEndReceived(msg);
        }

        // [추가] 피버/보스전 메시지 — MultiplayerRhythmManager에 위임
        private void OnFeverStartReceived(FeverStartMessage msg)
        {
            if (msg.MatchId != MatchId) return;
            MultiplayerRhythmManager.Instance?.OnFeverStartReceived(msg);
        }

        private void OnFeverUpdateReceived(FeverUpdateMessage msg)
        {
            if (msg.MatchId != MatchId) return;
            MultiplayerRhythmManager.Instance?.OnFeverUpdateReceived(msg);
        }

        private void OnFeverResultReceived(FeverResultMessage msg)
        {
            if (msg.MatchId != MatchId) return;
            MultiplayerRhythmManager.Instance?.OnFeverResultReceived(msg);
        }
    }
}
