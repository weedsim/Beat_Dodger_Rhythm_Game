using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.InputSystem;
using Mirror;
using BeatDodger.Network;

namespace BeatDodger.Multiplayer
{
    /// <summary>
    /// 멀티플레이어 인게임 클라이언트측 매니저.
    ///
    /// 기존 NewRhythmManager(로컬 전용)는 수정하지 않는다.
    /// 이 클래스는 멀티 전용으로 독립적으로 동작한다.
    ///
    /// ■ 데이터 흐름
    ///   GameSessionContext → OnNoteSpawnReceived() → NoteEnemy 로컬 스폰
    ///   GameSessionContext → OnPersonalJudgmentReceived() → UI 갱신
    ///   GameSessionContext → OnNoteHitResultReceived() → 다른 플레이어 결과 표시
    ///   [수정] OnNoteHitResultReceived()에 공유 피버 게이지 갱신 추가 (파티 전체, 자신 포함)
    ///   [추가] GameSessionContext → OnFeverStartReceived() → 보스 애니메이션 / 레이저 초기화
    ///   [추가] GameSessionContext → OnFeverUpdateReceived() → 레이저 클래시 위치 갱신
    ///   [추가] GameSessionContext → OnFeverResultReceived() → 보스/플레이어 결과 애니메이션
    ///
    /// ■ 서버와의 차이
    ///   노트는 서버가 스케줄하고 NoteSpawnMessage로 전송.
    ///   클라이언트는 수신 즉시 NoteEnemy.Initialize()로 로컬 스폰.
    ///   판정은 CmdHitNote로 서버에 보고 → TargetRpc로 결과 수신.
    ///   피버 게이지/콤보/판정은 서버가 관리하므로 클라이언트는 시각 연출만 담당.
    /// </summary>
    public class MultiplayerRhythmManager : MonoBehaviour
    {
        public static MultiplayerRhythmManager Instance { get; private set; }

        // [추가] 서버로부터 수신된 BPM. Update() beat loop 및 외부 참조용.
        public float BPM { get; private set; }

        // ──────────────────────────────────────────────────────────────────
        // Inspector 필드 — NewRhythmManager의 Header 구조와 동일하게 정렬
        // ──────────────────────────────────────────────────────────────────

        [Header("Fever Settings")]
        [SerializeField] private FeverGaugeController feverGaugeController;

        // [추가] Fever Phase Settings — NewRhythmManager와 동일 필드 추가
        [Header("Fever Phase Settings")]
        // [수정] 기존 _bossAnimator → backgroundBossAnimator (NewRhythmManager와 동일 필드명)
        [SerializeField] private Animator backgroundBossAnimator;
        // [수정] 기존 _playerAnimators → playerAnimators (NewRhythmManager와 동일 필드명)
        [SerializeField] private Animator[] playerAnimators;
        [SerializeField] private float enterAnimDuration = 2.0f; // [추가]
        [SerializeField] private float exitAnimDuration = 2.0f;  // [추가]
        [SerializeField] private GameObject feverBossExplosion;  // [추가]

        // [추가] Fever Success Effects — NewRhythmManager와 동일
        [Header("Fever Success Effects")]
        [SerializeField] private GameObject firstSuccessEffect;  // [추가] 1번째 피버 성공 이펙트
        [SerializeField] private GameObject secondSuccessEffect; // [추가] 2번째 피버 성공 이펙트

        [Header("Laser Duel Settings")]
        // [수정] 레이저 프리팹 — NewRhythmManager와 동일하게 플레이어/보스 양측 + 클래시 이펙트 3개로 분리
        //         기존 단일 _laserBeamObject 제거
        [SerializeField] private GameObject _playerLaserPrefab;
        [SerializeField] private GameObject _bossLaserPrefab;
        [SerializeField] private GameObject _laserClashEffectPrefab;
        [SerializeField] private Transform _playerLaserOrigin;
        [SerializeField] private Transform _bossLaserOrigin;

        [Tooltip("레이저 프리팹이 향하는 기본 축 (위쪽=Y, 앞쪽=Z)")]
        [SerializeField] private Vector3 _laserPrefabAxis = Vector3.up;
        [Tooltip("충돌 지점까지 레이저 길이를 스케일링할지 여부")]
        [SerializeField] private bool _stretchLasers = true;
        [Tooltip("플레이어 레이저 길이 보정값 (프리팹 형태/크기에 맞춰 조절)")]
        [SerializeField] private float _playerLaserMultiplier = 1.0f;
        [Tooltip("보스 레이저 길이 보정값 (프리팹 형태/크기에 맞춰 조절)")]
        [SerializeField] private float _bossLaserMultiplier = 1.0f;
        [Tooltip("플레이어가 밀렸을 때 플레이어 레이저를 숨길 비율 (0.0~1.0)")]
        [Range(0f, 1f)] [SerializeField] private float _playerLaserHideThreshold = 0.05f;
        [Tooltip("보스가 밀렸을 때 보스 레이저를 숨길 비율 (0.0~1.0)")]
        [Range(0f, 1f)] [SerializeField] private float _bossLaserHideThreshold = 0.95f;
        [Tooltip("레이저 격돌 시 카메라가 이동할 목표 위치/회전")]
        [SerializeField] private Transform laserDuelCameraTarget;         // [추가]
        [Tooltip("레이저 격돌 성공 후 이동할 클리어 전용 카메라 목표 위치/회전")]
        [SerializeField] private Transform laserDuelClearCameraTarget;    // [추가]
        [Tooltip("카메라 이동에 걸리는 시간")]
        [SerializeField] private float cameraTransitionDuration = 1.0f;  // [추가]

        // [추가] Fever Toggle Objects — NewRhythmManager와 동일
        [Header("Fever Toggle Objects")]
        [Tooltip("피버 모드 진입 시 켜질 오브젝트들 (피버 종료 시 다시 꺼짐)")]
        [SerializeField] private List<GameObject> feverEnableObjects = new List<GameObject>();
        [Tooltip("피버 모드 진입 시 꺼질 오브젝트들 (피버 종료 시 다시 켜짐)")]
        [SerializeField] private List<GameObject> feverDisableObjects = new List<GameObject>();

        // [추가] Boss Idle Animations — NewRhythmManager와 동일
        [Header("Boss Idle Animations")]
        [SerializeField] private float minAttackInterval = 4.0f;
        [SerializeField] private float maxAttackInterval = 8.0f;

        [Header("Note Prefabs")]
        [SerializeField] private GameObject normalPrefab;
        [SerializeField] private GameObject doublePrefab;
        [SerializeField] private GameObject dashPrefab;
        [Tooltip("같이치기 노트 사이에 생성될 연결 이펙트 프리팹")]
        [SerializeField] private GameObject chordConnectionEffect; // [추가]

        [Header("References")]
        [SerializeField] private LaneInputEffect[] inputEffects;

        [Header("Hit Effects")]
        [SerializeField] private GameObject hitEffectPerfect;
        [SerializeField] private GameObject hitEffectGood;

        // [추가] Audio — NewRhythmManager와 동일. 서버 DSP 기준 시간에 맞춰 PlayScheduled 호출.
        [Header("Audio")]
        [SerializeField] private AudioSource mainAudioSource;

        // ──────────────────────────────────────────────────────────────────
        // 오브젝트 풀 (기존 NewRhythmManager와 동일한 풀 패턴)
        // ──────────────────────────────────────────────────────────────────

        // NoteEnemy 풀 (기존 NewRhythmManager와 동일한 풀 패턴)
        private readonly Dictionary<NoteType, IObjectPool<NoteEnemy>> _notePools
            = new Dictionary<NoteType, IObjectPool<NoteEnemy>>();
        private IObjectPool<GameObject> _poolPerfect;
        private IObjectPool<GameObject> _poolGood;

        // 현재 활성 노트
        private readonly List<NoteEnemy> _activeNotes = new List<NoteEnemy>();

        // ──────────────────────────────────────────────────────────────────
        // 상태 (State)
        // ──────────────────────────────────────────────────────────────────

        // [수정] _feverGauge 제거 — 공유 피버 게이지는 NoteHitResultMessage.FeverGauge로 전체 파티에 브로드캐스트됨
        private bool _isGameActive;
        private int _combo;
        private int _maxCombo;         // [추가]
        private double _noteDuration;
        private int _beatsToArrive;
        private float _secondsPerBeat; // [추가] beat loop용
        private double _nextBeatTime;  // [추가] beat loop용
        private double _songStartTime; // [추가]
        public double SongStartTime => _songStartTime; // [추가]
        private bool _isSongPlaying;   // [추가]

        // [추가] isGameCleared — NewRhythmManager와 동일 패턴 (public readable, private settable)
        public bool isGameCleared { get; private set; } = false;

        // [추가] 피버 / 보스전 상태
        private bool _isFeverActive;
        private bool _isLaserDuel;
        private int _feverSuccessCount; // [추가] FeverResultMessage.FeverSuccessCount로 서버와 동기화

        // [추가] 레이저 결투 런타임 인스턴스 (진입 시 Instantiate, 종료 시 Destroy)
        private GameObject _playerLaserInstance;
        private GameObject _bossLaserInstance;
        private GameObject _clashInstance;

        // [추가] 피버 진입 전 카메라 위치 캐시 — 레이저 격돌 종료 후 복원용
        private Vector3 _preFeverCameraPos;
        private Quaternion _preFeverCameraRot;

        // [추가] 플레이어 캐릭터 초기 트랜스폼 캐시 — 피버 종료 후 Rebind 시 원복 (NewRhythmManager 동일 패턴)
        private Vector3[] _playerInitialPositions;
        private Quaternion[] _playerInitialRotations;

        // [추가] 보스 idle 공격 코루틴 참조 — 피버 시작/게임 종료 시 정지
        private Coroutine _randomAttackCoroutine;

        private const float EffectReturnDelaySeconds = 1.0f;
        private readonly WaitForSeconds effectReturnDelay = new WaitForSeconds(EffectReturnDelaySeconds);

        // ──────────────────────────────────────────────────────────────────
        // 이벤트 — NewRhythmManager와 동일. RhythmFloorMover 등이 구독.
        // ──────────────────────────────────────────────────────────────────

        // [추가] NewRhythmManager.OnBeat와 동일. RhythmFloorMover, RhythmColorSwitcher가 구독.
        public static event Action OnBeat;
        // [추가] 히트 결과 이벤트. ComboUIController 등이 구독.
        public event Action<Judgment, int> OnNoteHit;
        // [추가] 피버 상태 변경 이벤트.
        public static event Action<bool> OnFeverStateChanged;

        // ──────────────────────────────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────────────────────────────

        // [추가] NewRhythmManager.IsFeverTime과 동일. NoteEnemy 및 UI 시스템이 사용.
        public bool IsFeverTime => _isFeverActive;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // [추가] 씬에 고정 배치된 플레이어 캐릭터의 초기 트랜스폼 캐시
            //        피버 종료 후 Rebind() 시 루트 모션으로 밀린 위치를 여기로 복원한다.
            if (playerAnimators != null)
            {
                _playerInitialPositions = new Vector3[playerAnimators.Length];
                _playerInitialRotations = new Quaternion[playerAnimators.Length];
                for (int i = 0; i < playerAnimators.Length; i++)
                {
                    if (playerAnimators[i] != null)
                    {
                        _playerInitialPositions[i] = playerAnimators[i].transform.localPosition;
                        _playerInitialRotations[i] = playerAnimators[i].transform.localRotation;
                    }
                }
            }
        }

        private void Start()
        {
            SetupObjectPools();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ──────────────────────────────────────────────────────────────────
        // Update — beat loop (NewRhythmManager와 동일 패턴, 서버 시작 시간 기준)
        // [추가] 노트 스폰은 서버가 담당하므로 랜덤/차트 로직 없음.
        //        RhythmFloorMover·RhythmColorSwitcher는 OnBeat 구독으로 애니메이션 재생.
        // ──────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_isSongPlaying || isGameCleared) return;

            double currentTime = AudioSettings.dspTime;
            if (currentTime >= _nextBeatTime)
            {
                _nextBeatTime += _secondsPerBeat;
                OnBeat?.Invoke();
            }
        }

        // ───────────────────────────────────────────────────────────────
        // 입력 처리 — 멀티플레이어는 단일 키 입력 (스페이스바 기본)
        // [수정] 로컬(NewRhythmManager)과 달리 레인 구분 없이 키 하나로 모든 판정을 처리.
        //        InputActionAsset의 "Multiplayer" Action Map에 "Hit" Action 하나만 정의하고
        //        PlayerInput Inspector에서 이 메서드를 연결한다.
        //        차후 플레이어가 키를 변경할 수 있도록 InputActionAsset의 바인딩 오버라이드로 확장 예정.
        // ───────────────────────────────────────────────────────────────

        // [수정] OnInputLane0-3 4개 → 단일 OnHit으로 교체 (멀티는 키 하나로 입력)
        public void OnHit(InputAction.CallbackContext ctx) { if (ctx.performed) ExecuteInput(); }

        private void ExecuteInput()
        {
            if (!_isGameActive) return;

            // 피버 활성 중에는 연타 입력을 서버로 전송 (CmdHitNote 대신)
            if (_isFeverActive)
            {
                int matchId = GameSessionContext.Instance?.MatchId ?? -1;
                if (matchId >= 0)
                    NetworkClient.Send(new SubmitMashMessage { MatchId = matchId });
                return;
            }

            // [수정] 레인 무관 — 활성 노트 중 판정선에 가장 가까운 것을 서버에 보고
            NoteEnemy closest = FindClosestNoteAnyLane();
            if (closest == null) return;

            GameSessionContext.Instance?.LocalGamePlayer?.CmdHitNote(
                GameSessionContext.Instance.MatchId, closest.myNoteId, AudioSettings.dspTime);
        }

        // [수정] FindClosestNote(int laneIndex) → FindClosestNoteAnyLane()
        //        단일 입력이므로 레인 필터링 없이 시간적으로 가장 가까운 노트를 반환.
        private NoteEnemy FindClosestNoteAnyLane()
        {
            NoteEnemy closest = null;
            double minOffset = double.MaxValue;
            float goodThreshold = RhythmConfig.Instance != null
                ? RhythmConfig.Instance.GoodThreshold : 0.15f;

            foreach (NoteEnemy note in _activeNotes)
            {
                double offset = Math.Abs(AudioSettings.dspTime - note.TargetHitTime);
                if (offset <= goodThreshold && offset < minOffset)
                {
                    minOffset = offset;
                    closest = note;
                }
            }

            return closest;
        }

        // ───────────────────────────────────────────────────────────────
        // GameSessionContext로부터 호출되는 메시지 핸들러
        // ───────────────────────────────────────────────────────────────

        /// <summary>서버로부터 게임 시작 신호 수신</summary>
        public void OnGameStartReceived(GameStartMessage msg)
        {
            _isGameActive = true;
            _combo = 0;
            _maxCombo = 0;          // [추가]
            _isFeverActive = false;
            _isLaserDuel = false;
            _feverSuccessCount = 0; // [추가]
            isGameCleared = false;  // [추가]
            _beatsToArrive = msg.BeatsToArrive;

            // [추가] BPM/박자 계산 — Update() beat loop과 noteDuration에 사용
            BPM = msg.Bpm;
            _secondsPerBeat = 60f / msg.Bpm;
            _noteDuration = _secondsPerBeat * msg.BeatsToArrive;

            // [추가] 서버 DSP 기준 시간으로 beat loop 초기화 (NewRhythmManager.StartSong()과 동일 패턴)
            _songStartTime = msg.ServerStartDspTime;
            _nextBeatTime = _songStartTime + _secondsPerBeat;
            _isSongPlaying = true;

            // [추가] 음악 재생: 서버 DSP 기준 시간에 맞춰 스케줄 (NewRhythmManager.StartSong()과 동일)
            if (mainAudioSource != null)
                mainAudioSource.PlayScheduled(_songStartTime);

            // [추가] 플로어 리셋 (NewRhythmManager.StartSong()과 동일)
            FindFirstObjectByType<RhythmFloorMover>()?.ResetFloor();

            // [추가] 보스 idle 공격 코루틴 시작 (NewRhythmManager.StartSong()과 동일)
            if (_randomAttackCoroutine != null) StopCoroutine(_randomAttackCoroutine);
            _randomAttackCoroutine = StartCoroutine(RandomBossAttackRoutine());

            Debug.Log($"[MultiplayerRhythmManager] 게임 시작 — BPM: {msg.Bpm}, BeatsToArrive: {msg.BeatsToArrive}");
        }

        /// <summary>서버로부터 노트 스폰 메시지 수신 → 로컬 스폰</summary>
        public void OnNoteSpawnReceived(NoteSpawnMessage msg)
        {
            NoteType type = msg.NoteType;

            if (!_notePools.TryGetValue(type, out IObjectPool<NoteEnemy> pool))
            {
                if (!_notePools.TryGetValue(NoteType.Normal, out pool)) return;
            }

            // [수정] span > 1인 경우(같이치기 노트) 개별 노트 여러 개 스폰 + 연결 이펙트 추가
            //        NewRhythmManager.SpawnIndividualNote()와 동일 패턴
            if (msg.Span > 1)
            {
                int totalLanes = RhythmConfig.Instance != null ? RhythmConfig.Instance.LaneCount : 4;
                NoteEnemy firstNote = null;

                for (int i = 0; i < msg.Span; i++)
                {
                    NoteEnemy n = SpawnSingleNote(pool, msg.NoteId + i, msg.Lane + i, 1, msg.HitTime, type);
                    if (i == 0) firstNote = n;
                }

                // [추가] 같이치기 노트 연결 이펙트 (NewRhythmManager.SpawnIndividualNote()와 동일)
                if (chordConnectionEffect != null && firstNote != null && RhythmConfig.Instance != null)
                {
                    float startX = (msg.Lane - (totalLanes / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
                    float endX = (msg.Lane + msg.Span - 1 - (totalLanes / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
                    // 부모(노트)의 회전(Y=180 등)에 의해 좌우가 반전되는 것을 막기 위해 월드 좌표 오프셋으로 전달
                    Vector3 worldOffset = new Vector3((startX + endX) / 2f - startX, 0, 0);
                    firstNote.AddConnectionEffect(chordConnectionEffect, worldOffset, msg.Span);
                }
            }
            else
            {
                SpawnSingleNote(pool, msg.NoteId, msg.Lane, msg.Span, msg.HitTime, type);
            }

            Debug.Log($"[MultiplayerRhythmManager] 노트 스폰 — Id: {msg.NoteId}, Lane: {msg.Lane}, Span: {msg.Span}, HitTime: {msg.HitTime:F3}");
        }

        // [추가] 단일 노트 스폰 내부 헬퍼
        private NoteEnemy SpawnSingleNote(IObjectPool<NoteEnemy> pool, int noteId, int lane, int span, double hitTime, NoteType type)
        {
            NoteEnemy note = pool.Get();
            note.myNoteId = noteId;
            note.Initialize(pool, lane, span, hitTime, (float)_noteDuration, _beatsToArrive, type);
            return note;
        }

        /// <summary>TargetRpc를 통한 개인 판정 결과 수신</summary>
        public void OnPersonalJudgmentReceived(int noteId, Judgment judgment, int combo, float feverGauge)
        {
            _combo = combo;
            _maxCombo = Math.Max(_maxCombo, _combo); // [추가]

            // 판정 UI 갱신
            int mySlot = GameSessionContext.Instance?.SlotIndex ?? 0;
            JudgmentUIController.Instance?.DisplayJudgment(mySlot % 4, judgment, false);

            // 피버 게이지 갱신
            // [수정] 피버 게이지는 NoteHitResultMessage(공유 게이지)로 전체 파티가 갱신하므로 여기서 제거
            // feverGaugeController?.UpdateFeverGauge(feverGauge, 100f);

            // 히트 이펙트
            SpawnHitEffect(judgment, mySlot % 4);

            // [추가] 히트 이벤트 발화 (ComboUIController 등 구독자에게 알림)
            OnNoteHit?.Invoke(judgment, _combo);

            // 콤보 UI는 기존 ComboUIController 이벤트 시스템 대신 직접 갱신
            // (NewRhythmManager.OnNoteHit 이벤트와 독립적으로 동작)
        }

        /// <summary>다른 플레이어의 히트 결과 수신 (파티 전체 브로드캐스트)</summary>
        public void OnNoteHitResultReceived(NoteHitResultMessage msg)
        {
            // 내 히트 결과는 TargetRpc로 이미 처리했으므로 다른 플레이어 것만 처리
            // [수정] 공유 피버 게이지(msg.FeverGauge)는 자신 포함 전체 파티원이 갱신
            feverGaugeController?.UpdateFeverGauge(msg.FeverGauge, 100f);

            uint myNetId = GameSessionContext.Instance?.LocalGamePlayer?.netId ?? 0;
            if (myNetId != 0 && msg.HitterNetId == myNetId) return;

            // 다른 플레이어의 히트로 해당 노트를 화면에서 제거
            RemoveNoteById(msg.NoteId);

            Debug.Log($"[MultiplayerRhythmManager] 다른 플레이어 히트 — NetId: {msg.HitterNetId}, 판정: {msg.Judgment}");
        }

        /// <summary>파티 공유 피버 진입. 보스 애니메이션과 레이저 초기화. [추가]</summary>
        public void OnFeverStartReceived(FeverStartMessage msg)
        {
            _isFeverActive = true;
            _isLaserDuel = msg.FeverStage >= 3;

            // [추가] 피버 모드 진입 시 오브젝트 켜기/끄기 (NewRhythmManager.SetFeverState(true)와 동일)
            if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(true);
            if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(false);

            // [추가] 화면에 남은 일반 노트들 클리어 (NewRhythmManager.SetFeverState(true)와 동일)
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
                _activeNotes[i].ReleaseToPool();

            // [수정] NewRhythmManager와 동일 — 레이저 결투 시 "Attack01", 일반 피버 시 "FallDown"
            backgroundBossAnimator?.SetTrigger(_isLaserDuel ? "Attack01" : "FallDown");

            // [추가] 카메라 위치 캐시 후 이동 (NewRhythmManager.FeverSequenceCoroutine과 동일)
            if (Camera.main != null)
            {
                _preFeverCameraPos = Camera.main.transform.position;
                _preFeverCameraRot = Camera.main.transform.rotation;

                if (_isLaserDuel && laserDuelCameraTarget != null)
                    StartCoroutine(MoveCameraCoroutine(laserDuelCameraTarget.position, laserDuelCameraTarget.rotation, cameraTransitionDuration));
            }

            if (_isLaserDuel)
            {
                // [수정] 단일 _laserBeamObject 대신 플레이어/보스 양측 레이저 + 클래시 이펙트 각각 인스턴스화
                if (_playerLaserPrefab != null && _playerLaserOrigin != null)
                    _playerLaserInstance = Instantiate(_playerLaserPrefab, _playerLaserOrigin.position, _playerLaserOrigin.rotation);
                if (_bossLaserPrefab != null && _bossLaserOrigin != null)
                    _bossLaserInstance = Instantiate(_bossLaserPrefab, _bossLaserOrigin.position, _bossLaserOrigin.rotation);
                if (_laserClashEffectPrefab != null)
                {
                    _clashInstance = Instantiate(_laserClashEffectPrefab, Vector3.zero, _laserClashEffectPrefab.transform.rotation);
                    // 격돌 이펙트(SoundOrb 등)가 레이저 메시에 파묻히지 않도록 렌더링 순위를 최상단으로 강제
                    Renderer[] renderers = _clashInstance.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers) r.sortingOrder = 32000;
                }
                UpdateLaserPosition(0.5f);
            }

            // [추가] 이벤트 발화 — UI 시스템에 피버 진입 알림 (NewRhythmManager.SetFeverState()와 동일)
            OnFeverStateChanged?.Invoke(true);

            Debug.Log($"[MultiplayerRhythmManager] 피버 시작 — Stage: {msg.FeverStage}, Duration: {msg.Duration:F1}s");
        }

        /// <summary>피버 진행 중 레이저 클래시 위치 갱신. 레이저 결투 전용. [추가]</summary>
        public void OnFeverUpdateReceived(FeverUpdateMessage msg)
        {
            if (!_isFeverActive || !_isLaserDuel) return;
            UpdateLaserPosition(msg.Progress);
        }

        /// <summary>피버 종료 결과 수신. 성공/실패 애니메이션 및 게임 클리어 처리. [추가]</summary>
        public void OnFeverResultReceived(FeverResultMessage msg)
        {
            _isFeverActive = false;
            _feverSuccessCount = msg.FeverSuccessCount; // [추가] 서버 기준 누적 성공 수 동기화

            // [수정] SetActive 대신 Destroy — 런타임에 인스턴스화된 레이저 3개 모두 파괴
            if (_playerLaserInstance != null) { Destroy(_playerLaserInstance); _playerLaserInstance = null; }
            if (_bossLaserInstance   != null) { Destroy(_bossLaserInstance);   _bossLaserInstance   = null; }
            if (_clashInstance       != null) { Destroy(_clashInstance);        _clashInstance       = null; }

            Vector3 spawnPosition = backgroundBossAnimator != null
                ? backgroundBossAnimator.transform.position : Vector3.zero;

            if (msg.Success)
            {
                // [수정] 레이저 결투 성공: "Die" / 일반 피버 성공: "TakeDamage" (NewRhythmManager.FeverSequenceCoroutine과 동일)
                backgroundBossAnimator?.SetTrigger(_isLaserDuel ? "Die" : "TakeDamage");

                // [추가] 피버 성공 횟수별 이펙트 (NewRhythmManager.FeverSequenceCoroutine과 동일)
                if (_feverSuccessCount == 1)
                {
                    if (firstSuccessEffect != null) Instantiate(firstSuccessEffect, spawnPosition, firstSuccessEffect.transform.rotation);
                }
                else if (_feverSuccessCount == 2)
                {
                    if (secondSuccessEffect != null) Instantiate(secondSuccessEffect, spawnPosition, secondSuccessEffect.transform.rotation);
                }
                else if (_feverSuccessCount >= 3)
                {
                    if (feverBossExplosion != null) Instantiate(feverBossExplosion, spawnPosition, feverBossExplosion.transform.rotation);
                }

                // [추가] 피버 성공 플레이어 Victory 애니메이션 (NewRhythmManager.FeverSequenceCoroutine과 동일)
                if (playerAnimators != null && playerAnimators.Length > 0)
                {
                    string triggerName = UnityEngine.Random.Range(0, 2) == 0 ? "Victory 1" : "Victory 2";
                    foreach (var anim in playerAnimators)
                        if (anim != null) anim.SetTrigger(triggerName);
                }

                if (msg.GameCleared)
                {
                    isGameCleared = true; // [추가]
                    StartCoroutine(TriggerGameClear());
                    return; // 게임 클리어 → Rebind 없이 그대로 종료
                }
            }
            else
            {
                // [수정] 레이저 결투 실패: "Victory" / 일반 피버 실패: "Recover" (NewRhythmManager.FeverSequenceCoroutine과 동일)
                backgroundBossAnimator?.SetTrigger(_isLaserDuel ? "Victory" : "Recover");

                // [추가] 피버 실패 플레이어 Hit 애니메이션 (NewRhythmManager.FeverSequenceCoroutine과 동일)
                if (playerAnimators != null)
                    foreach (var anim in playerAnimators)
                        if (anim != null) anim.SetTrigger("Hit");
            }

            // [추가] 레이저 격돌 카메라 복구 (NewRhythmManager.FeverSequenceCoroutine과 동일)
            if (_isLaserDuel && Camera.main != null)
                StartCoroutine(MoveCameraCoroutine(_preFeverCameraPos, _preFeverCameraRot, cameraTransitionDuration));

            // [추가] 게임 클리어가 아닌 피버 종료(1·2번째) — Rebind + 루트 모션으로 밀린 위치 복원
            //        Awake()에서 캐시한 초기 localPosition/Rotation으로 되돌린다.
            if (playerAnimators != null)
            {
                for (int i = 0; i < playerAnimators.Length; i++)
                {
                    if (playerAnimators[i] != null)
                    {
                        playerAnimators[i].Rebind();
                        if (_playerInitialPositions != null && i < _playerInitialPositions.Length)
                        {
                            playerAnimators[i].transform.localPosition = _playerInitialPositions[i];
                            playerAnimators[i].transform.localRotation = _playerInitialRotations[i];
                        }
                    }
                }
            }

            // [추가] 피버 모드 토글 오브젝트 원상 복구 (NewRhythmManager.ResumeChartAfterFever()와 동일)
            if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(false);
            if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(true);

            // [추가] 이벤트 발화 — UI 시스템에 피버 종료 알림 (NewRhythmManager.ResumeChartAfterFever()와 동일)
            OnFeverStateChanged?.Invoke(false);

            Debug.Log($"[MultiplayerRhythmManager] 피버 결과 — 성공: {msg.Success}, 클리어: {msg.GameCleared}, 누적 성공: {msg.FeverSuccessCount}");
        }

        /// <summary>게임 종료 메시지 수신</summary>
        public void OnGameEndReceived(GameEndMessage msg)
        {
            _isGameActive = false;
            _isFeverActive = false; // [추가] 피버 상태 초기화
            _isSongPlaying = false; // [추가]

            // [추가] 보스 idle 공격 코루틴 정지
            if (_randomAttackCoroutine != null)
            {
                StopCoroutine(_randomAttackCoroutine);
                _randomAttackCoroutine = null;
            }

            Debug.Log($"[MultiplayerRhythmManager] 게임 종료 — 사유: {msg.Reason}");

            // [추가] 매치 컨텍스트 초기화 후 로비 패널로 복귀
            GameSessionContext.Instance?.Reset();
            BeatDodger.UI.UIManager.Instance?.TransitionToLobby();
        }

        /// <summary>개인 이벤트 수신 (TargetRpc)</summary>
        public void OnPersonalEventReceived(string eventKey)
        {
            Debug.Log($"[MultiplayerRhythmManager] 개인 이벤트: {eventKey}");
        }

        // ──────────────────────────────────────────────────────────────────
        // NoteEnemy에서 호출 — 노트가 판정선을 지나쳤을 때 미스 처리
        // [추가] NewRhythmManager.ReportMiss()와 동일 역할.
        //        멀티플레이어에서는 콤보/게이지는 서버가 관리하므로 시각 연출만 수행.
        //        NoteEnemy가 이 메서드를 호출하려면 GameSessionContext.IsMultiplayerMode 분기 추가 필요.
        // ──────────────────────────────────────────────────────────────────

        public void ReportMiss(NoteEnemy note) // [추가]
        {
            bool isFeverNote = note != null && note.Type == NoteType.Fever;
            if (IsFeverTime || isFeverNote) return;

            PlayPlayerHitAnimation();

            // 미스 판정 UI 표시 (NewRhythmManager.ReportMiss()와 동일)
            if (note != null)
            {
                for (int i = note.StartLane; i < note.StartLane + note.LaneSpan; i++)
                {
                    if (!note.IsLaneAlreadyHit(i))
                        JudgmentUIController.Instance?.DisplayJudgment(i, Judgment.Miss, false);
                }
            }

            OnNoteHit?.Invoke(Judgment.Miss, _combo);
            Debug.Log("[MultiplayerRhythmManager] Miss!");
        }

        // ──────────────────────────────────────────────────────────────────
        // 내부 구현
        // ──────────────────────────────────────────────────────────────────

        // [추가] NewRhythmManager.PlayPlayerHitAnimation()과 동일
        private void PlayPlayerHitAnimation()
        {
            if (playerAnimators != null)
                foreach (var anim in playerAnimators)
                    if (anim != null) anim.SetTrigger("Hit");
        }

        // [추가] NewRhythmManager.RandomBossAttackRoutine()과 동일.
        //        멀티플레이어에서도 보스 idle 공격 애니메이션은 클라이언트 측에서 독립적으로 재생.
        private IEnumerator RandomBossAttackRoutine()
        {
            while (true)
            {
                float waitTime = UnityEngine.Random.Range(minAttackInterval, maxAttackInterval);
                yield return new WaitForSeconds(waitTime);

                if (!IsFeverTime && backgroundBossAnimator != null)
                {
                    // 1 또는 2를 랜덤하게 선택하여 트리거 발동
                    int attackType = UnityEngine.Random.Range(1, 3);
                    backgroundBossAnimator.SetTrigger(attackType == 1 ? "Attack01" : "Attack02");
                }
            }
        }

        // [추가] NewRhythmManager.MoveCameraCoroutine()과 동일
        private IEnumerator MoveCameraCoroutine(Vector3 targetPos, Quaternion targetRot, float duration)
        {
            if (Camera.main == null) yield break;

            Transform camTransform = Camera.main.transform;
            Vector3 startPos = camTransform.position;
            Quaternion startRot = camTransform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                camTransform.position = Vector3.Lerp(startPos, targetPos, t);
                camTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            camTransform.position = targetPos;
            camTransform.rotation = targetRot;
        }

        // [추가] 게임 클리어 연출 후 결과 화면 전환
        private IEnumerator TriggerGameClear()
        {
            // 클리어 전용 카메라 앵글로 이동 (NewRhythmManager.FeverSequenceCoroutine과 동일)
            if (laserDuelClearCameraTarget != null && Camera.main != null)
                yield return StartCoroutine(MoveCameraCoroutine(laserDuelClearCameraTarget.position, laserDuelClearCameraTarget.rotation, cameraTransitionDuration));

            yield return new WaitForSeconds(2f);
            Debug.Log("[MultiplayerRhythmManager] 게임 클리어!");
            // TODO: UIManager.Instance?.TransitionToResult()
        }

        // [수정] progress 0 = 보스 측 우세, 1 = 플레이어 측 우세 — NewRhythmManager와 동일한 방향·스케일 로직
        //        기존 단순 Transform.Lerp 방식에서 레이저 회전·스트레칭 방식으로 변경
        private void UpdateLaserPosition(float progress)
        {
            if (_playerLaserOrigin == null || _bossLaserOrigin == null) return;

            // 클래시 포인트: progress 0 → 플레이어 스폰 위치(보스 우세), 1 → 보스 스폰 위치(플레이어 우세)
            Vector3 clashPos = Vector3.Lerp(_playerLaserOrigin.position, _bossLaserOrigin.position, progress);

            bool hidePlayerLaser = progress <= _playerLaserHideThreshold;
            bool hideBossLaser   = progress >= _bossLaserHideThreshold;
            bool hideClash       = hidePlayerLaser || hideBossLaser;

            // 클래시 이펙트 위치 갱신
            if (_clashInstance != null)
            {
                if (_clashInstance.activeSelf == hideClash) _clashInstance.SetActive(!hideClash);
                _clashInstance.transform.position = clashPos;
            }

            // 플레이어 레이저: 스폰 포인트 → 클래시 포인트 방향으로 회전·스케일
            if (_playerLaserInstance != null)
            {
                if (_playerLaserInstance.activeSelf == hidePlayerLaser) _playerLaserInstance.SetActive(!hidePlayerLaser);
                if (!hidePlayerLaser)
                {
                    Vector3 pDir = clashPos - _playerLaserOrigin.position;
                    if (pDir != Vector3.zero)
                    {
                        _playerLaserInstance.transform.position = _playerLaserOrigin.position;
                        _playerLaserInstance.transform.rotation = Quaternion.FromToRotation(_laserPrefabAxis, pDir);
                        if (_stretchLasers)
                        {
                            Vector3 scale = _playerLaserInstance.transform.localScale;
                            float len = pDir.magnitude * _playerLaserMultiplier;
                            if (_laserPrefabAxis == Vector3.up)          scale.y = len;
                            else if (_laserPrefabAxis == Vector3.forward) scale.z = len;
                            else if (_laserPrefabAxis == Vector3.right)   scale.x = len;
                            _playerLaserInstance.transform.localScale = scale;
                        }
                    }
                }
            }

            // 보스 레이저: 스폰 포인트 → 클래시 포인트 방향으로 회전·스케일
            if (_bossLaserInstance != null)
            {
                if (_bossLaserInstance.activeSelf == hideBossLaser) _bossLaserInstance.SetActive(!hideBossLaser);
                if (!hideBossLaser)
                {
                    Vector3 bDir = clashPos - _bossLaserOrigin.position;
                    if (bDir != Vector3.zero)
                    {
                        _bossLaserInstance.transform.position = _bossLaserOrigin.position;
                        _bossLaserInstance.transform.rotation = Quaternion.FromToRotation(_laserPrefabAxis, bDir);
                        if (_stretchLasers)
                        {
                            Vector3 scale = _bossLaserInstance.transform.localScale;
                            float len = bDir.magnitude * _bossLaserMultiplier;
                            if (_laserPrefabAxis == Vector3.up)          scale.y = len;
                            else if (_laserPrefabAxis == Vector3.forward) scale.z = len;
                            else if (_laserPrefabAxis == Vector3.right)   scale.x = len;
                            _bossLaserInstance.transform.localScale = scale;
                        }
                    }
                }
            }
        }

        private void RemoveNoteById(int noteId)
        {
            for (int i = _activeNotes.Count - 1; i >= 0; i--)
            {
                if (_activeNotes[i].myNoteId == noteId)
                {
                    _activeNotes[i].ReleaseToPool();
                    break;
                }
            }
        }

        private void SpawnHitEffect(Judgment judgment, int laneIndex)
        {
            IObjectPool<GameObject> pool = (judgment == Judgment.Perfect || judgment == Judgment.Great)
                ? _poolPerfect : _poolGood;

            if (pool == null) return;
            if (RhythmConfig.Instance == null) return;

            GameObject effect = pool.Get();
            float xPos = (laneIndex - (RhythmConfig.Instance.LaneCount / 2f - 0.5f))
                         * RhythmConfig.Instance.LaneSpacing;
            effect.transform.position = new Vector3(xPos, 0.1f, RhythmConfig.Instance.JudgeLineZ);
            StartCoroutine(ReturnEffectToPool(effect, pool));
        }

        private IEnumerator ReturnEffectToPool(GameObject effect, IObjectPool<GameObject> pool)
        {
            yield return effectReturnDelay;
            pool.Release(effect);
        }

        private void SetupObjectPools()
        {
            InitNotePool(NoteType.Normal, normalPrefab);
            InitNotePool(NoteType.Double, doublePrefab);
            InitNotePool(NoteType.Dash, dashPrefab);

            if (hitEffectPerfect != null)
            {
                _poolPerfect = new ObjectPool<GameObject>(
                    () => Instantiate(hitEffectPerfect),
                    g => g.SetActive(true),
                    g => g.SetActive(false),
                    g => Destroy(g),
                    false, 5, 10);
            }

            if (hitEffectGood != null)
            {
                _poolGood = new ObjectPool<GameObject>(
                    () => Instantiate(hitEffectGood),
                    g => g.SetActive(true),
                    g => g.SetActive(false),
                    g => Destroy(g),
                    false, 5, 10);
            }
        }

        private void InitNotePool(NoteType type, GameObject prefab)
        {
            if (prefab == null) return;

            var pool = new ObjectPool<NoteEnemy>(
                createFunc: () =>
                {
                    var go = Instantiate(prefab);
                    return go.TryGetComponent<NoteEnemy>(out var n) ? n : go.AddComponent<NoteEnemy>();
                },
                actionOnGet: note =>
                {
                    note.gameObject.SetActive(true);
                    _activeNotes.Add(note);
                },
                actionOnRelease: note =>
                {
                    note.gameObject.SetActive(false);
                    _activeNotes.Remove(note);
                },
                actionOnDestroy: note => Destroy(note.gameObject),
                collectionCheck: false, defaultCapacity: 10, maxSize: 30);

            _notePools[type] = pool;
        }

        // 테스트용: Inspector에서 강제 피버 진입은 서버 권한이므로 로컬에서 직접 트리거 불가
        private void OnGUI()
        {
            if (!_isGameActive) return;
            GUI.Label(new Rect(10, 10, 300, 25), $"[MP] Combo: {_combo} / Max: {_maxCombo} / Fever: {_isFeverActive}");
        }
    }
}
