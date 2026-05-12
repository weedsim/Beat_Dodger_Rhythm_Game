using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.InputSystem;
using Mirror;

public class NewRhythmManager : MonoBehaviour
{
    public static NewRhythmManager Instance { get; private set; }

    [Header("Multiplayer Sync")]

    public double exactStartTime; // 서버에서 정해줄 절대 시작 시간
    public bool isGameStart = false; // 게임 시작 여부

    [Header("Rhythm Settings")]
    [SerializeField] private float bpm = 120f;
    public float BPM => bpm;
    [SerializeField] private int beatsToArrive = 4;
    [SerializeField] private float startDelay = 2.0f; // Seconds to wait before music starts
    public int BeatsToArrive => beatsToArrive;

    [Header("Fever Settings")]
    [SerializeField] private float gaugePerHit = 10f;
    [SerializeField] private float gaugeLossOnMiss = 5f;
    [SerializeField] private FeverGaugeController feverGaugeController;

    [Header("Fever Phase Settings")]
    [SerializeField] private Animator backgroundBossAnimator; // 배경의 거대 보스 애니메이터
    [SerializeField] private Animator[] playerAnimators; // 플레이어 애니메이터 배열
    [SerializeField] private float enterAnimDuration = 2.0f;
    [SerializeField] private float mashingDuration = 5.0f;
    [SerializeField] private float exitAnimDuration = 2.0f;
    [SerializeField] private int requiredMashCount = 50;
    [SerializeField] private GameObject feverBossExplosion;

    [Header("Chart Settings")]
    [SerializeField] private RhythmChart currentChart; // 채보 파일
    public int currentNoteIndex = 0; // 현재 읽고 있는 노트 번호

    [Header("Fever Success Effects")]
    [SerializeField] private GameObject firstSuccessEffect;
    [SerializeField] private GameObject secondSuccessEffect;
    private int feverSuccessCount = 0;

    [Header("Laser Duel Settings")]
    [SerializeField] private int laserDuelRequiredMashCount = 80;
    [SerializeField] private float laserDuelDuration = 8.0f;
    [SerializeField] private float bossPushSpeed = 10f; // 보스가 초당 밀어내는 연타치
    [SerializeField] private GameObject playerLaserEffect;
    [SerializeField] private GameObject bossLaserEffect;
    [SerializeField] private GameObject laserClashEffect;
    [SerializeField] private Transform playerLaserSpawnPoint;
    [SerializeField] private Transform bossLaserSpawnPoint;

    [Tooltip("레이저 프리팹이 향하는 기본 축 (위쪽=Y, 앞쪽=Z)")]
    [SerializeField] private Vector3 laserPrefabAxis = Vector3.up;
    [Tooltip("충돌 지점까지 레이저 길이를 스케일링할지 여부")]
    [SerializeField] private bool stretchLasers = true;
    [Tooltip("플레이어 레이저 길이 보정값 (프리팹 형태/크기에 맞춰 조절)")]
    [SerializeField] private float playerLaserMultiplier = 1.0f;
    [Tooltip("보스 레이저 길이 보정값 (프리팹 형태/크기에 맞춰 조절)")]
    [SerializeField] private float bossLaserMultiplier = 1.0f;
    [Tooltip("플레이어가 밀렸을 때 플레이어 레이저를 숨길 비율 (0.0~1.0, 예: 0.05)")]
    [Range(0f, 1f)] [SerializeField] private float playerLaserHideThreshold = 0.05f;
    [Tooltip("보스가 밀렸을 때 보스 레이저를 숨길 비율 (0.0~1.0, 예: 0.95)")]
    [Range(0f, 1f)] [SerializeField] private float bossLaserHideThreshold = 0.95f;
    [Tooltip("레이저 격돌 시 카메라가 이동할 목표 위치/회전")]
    [SerializeField] private Transform laserDuelCameraTarget;
    [Tooltip("레이저 격돌 성공 후 이동할 클리어 전용 카메라 목표 위치/회전")]
    [SerializeField] private Transform laserDuelClearCameraTarget;
    [Tooltip("카메라 이동에 걸리는 시간")]
    [SerializeField] private float cameraTransitionDuration = 1.0f;

    [Header("Fever Toggle Objects")]
    [Tooltip("피버 모드 진입 시 켜질 오브젝트들 (피버 종료 시 다시 꺼짐)")]
    [SerializeField] private List<GameObject> feverEnableObjects = new List<GameObject>();
    [Tooltip("피버 모드 진입 시 꺼질 오브젝트들 (피버 종료 시 다시 켜짐)")]
    [SerializeField] private List<GameObject> feverDisableObjects = new List<GameObject>();

    [Header("Boss Idle Animations")]
    [SerializeField] private float minAttackInterval = 4.0f;
    [SerializeField] private float maxAttackInterval = 8.0f;


    public enum SpawnMode { Random, Chart }
    [Header("Spawn Mode")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Random;
    [SerializeField] private RhythmChart chartAsset;
    [SerializeField] private AudioSource mainAudioSource;

    public enum FeverState { None, EnterAnimation, Mashing, ExitAnimation }
    private FeverState currentFeverState = FeverState.None;

    public int myLaneIndex = 0;
    private int currentMashCount;
    private float currentMashFloat; // float based mash count for laser duel
    private float currentFeverGauge;
    private bool isWaitingToResume;
    private Coroutine randomAttackCoroutine;

    private const float MaxFeverGauge = 100f;
    private const float ResumeDelaySeconds = 1.5f;
    private const float EffectReturnDelaySeconds = 1.0f;

    private readonly WaitForSeconds resumeDelay = new WaitForSeconds(ResumeDelaySeconds);
    private readonly WaitForSeconds effectReturnDelay = new WaitForSeconds(EffectReturnDelaySeconds);

    public bool IsFeverTime => currentFeverState != FeverState.None;
    public float FeverProgress => Mathf.Clamp01(currentFeverGauge / MaxFeverGauge);

    [Header("References")]
    [Header("Note Prefabs")]
    [SerializeField] private GameObject normalPrefab;
    [SerializeField] private GameObject doublePrefab;
    [SerializeField] private GameObject dashPrefab;
    [Tooltip("같이치기 노트 사이에 생성될 연결 이펙트 프리팹")]
    [SerializeField] private GameObject chordConnectionEffect;
    [SerializeField] private LaneInputEffect[] inputEffects;
    [SerializeField] private GameObject hitEffectPerfect; // For Perfect & Great
    [SerializeField] private GameObject hitEffectGood;    // For Good

    private Dictionary<NoteType, IObjectPool<NoteEnemy>> notePools = new Dictionary<NoteType, IObjectPool<NoteEnemy>>();
    private IObjectPool<GameObject> poolPerfect;
    private IObjectPool<GameObject> poolGood;
    public readonly List<NoteEnemy> activeNotes = new List<NoteEnemy>();

    // State Management
    private int currentCombo;
    private int maxCombo;
    private float secondsPerBeat;
    private float noteDuration;
    private double nextBeatTime;
    private double songStartTime;
    public double SongStartTime => songStartTime;
    private RhythmChart loadedChart;
    private int nextNoteIndex;
    private bool isSongPlaying;
    public bool isGameCleared { get; private set; } = false;

    private Vector3[] playerInitialPositions;
    private Quaternion[] playerInitialRotations;

    // Events (UI and System integration)
    public static event Action OnBeat;
    public event Action<Judgment, int> OnNoteHit;
    public static event Action<bool> OnFeverStateChanged;
    private HashSet<int> hitNoteIds = new HashSet<int>();
    [Header("Multiplayer ID")]
    private int _globalNoteId = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad 쓰고 있다면 여기에
        }
        else
        {
            Debug.LogWarning("RhythmManager 중복 감지! 파괴됨"); // ← 이걸로 확인
            Destroy(gameObject);
            return; // 중요! 이하 초기화 코드 실행 방지
        }

        InitializeRhythmSettings();
        SetupObjectPool();
        LoadChartIfNecessary();
    }

    private void LoadChartIfNecessary()
    {
        if (spawnMode == SpawnMode.Chart && chartAsset != null)
        {
            loadedChart = chartAsset;
            loadedChart.SortNotes(); // 강제로 시간순 정렬
            bpm = loadedChart.bpm;

            // Auto-assign clip if AudioSource exists but has no clip
            if (mainAudioSource != null && mainAudioSource.clip == null)
            {
                mainAudioSource.clip = loadedChart.musicClip;
            }

            InitializeRhythmSettings();
        }
    }

    public void StartSong()
    {
        songStartTime = exactStartTime;
        nextNoteIndex = 0;
        isSongPlaying = true;

        if (mainAudioSource != null)
        {
            mainAudioSource.PlayScheduled(songStartTime);
        }

        // Reset floor mover if exists
        FindFirstObjectByType<RhythmFloorMover>()?.ResetFloor();

        nextBeatTime = songStartTime + secondsPerBeat;

        // Start random boss attacks
        if (randomAttackCoroutine != null) StopCoroutine(randomAttackCoroutine);
        randomAttackCoroutine = StartCoroutine(RandomBossAttackRoutine());
    }

    private System.Collections.IEnumerator RandomBossAttackRoutine()
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

    private void InitializeRhythmSettings()
    {
        secondsPerBeat = 60f / bpm;
        noteDuration = secondsPerBeat * beatsToArrive;
    }

    private void SetupObjectPool()
    {
        InitializeNotePool(NoteType.Normal, normalPrefab);
        InitializeNotePool(NoteType.Double, doublePrefab);
        InitializeNotePool(NoteType.Dash, dashPrefab);

        // Perfect & Great Effect Pool
        poolPerfect = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(hitEffectPerfect),
            actionOnGet: (go) => go.SetActive(true),
            actionOnRelease: (go) => go.SetActive(false),
            actionOnDestroy: (go) => Destroy(go),
            collectionCheck: false, defaultCapacity: 5, maxSize: 10
        );

        // Good Effect Pool
        poolGood = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(hitEffectGood),
            actionOnGet: (go) => go.SetActive(true),
            actionOnRelease: (go) => go.SetActive(false),
            actionOnDestroy: (go) => Destroy(go),
            collectionCheck: false, defaultCapacity: 5, maxSize: 10
        );

        if (feverBossExplosion != null)
        {
            // Optional: Pool for explosion if reused
        }
    }

    private void InitializeNotePool(NoteType type, GameObject prefab)
    {
        if (prefab == null) return;

        var pool = new ObjectPool<NoteEnemy>(
            createFunc: () =>
            {
                GameObject instance = Instantiate(prefab);
                return instance.TryGetComponent<NoteEnemy>(out NoteEnemy note) ? note : instance.AddComponent<NoteEnemy>();
            },
            actionOnGet: (note) =>
            {
                if (note == null || note.gameObject == null) return;
                note.gameObject.SetActive(true);
                activeNotes.Add(note);
            },
            actionOnRelease: (note) =>
            {
                if (note == null || note.gameObject == null) return;
                note.gameObject.SetActive(false);
                activeNotes.Remove(note);
            },
            actionOnDestroy: (note) =>
            {
                if (note == null || note.gameObject == null) return;
                Destroy(note.gameObject);
            },
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 30
        );

        notePools[type] = pool;
    }

    private void OnDestroyNoteFromPool(NoteEnemy noteEnemy) { Destroy(noteEnemy.gameObject); }

    private void Start()
    {
        Debug.Log(" 신호탄: 리듬 매니저가 씬에 태어났습니다! ");
        if (playerAnimators != null)
        {
            playerInitialPositions = new Vector3[playerAnimators.Length];
            playerInitialRotations = new Quaternion[playerAnimators.Length];
            for (int i = 0; i < playerAnimators.Length; i++)
            {
                if (playerAnimators[i] != null)
                {
                    playerInitialPositions[i] = playerAnimators[i].transform.localPosition;
                    playerInitialRotations[i] = playerAnimators[i].transform.localRotation;
                }
            }
        }

        if (spawnMode == SpawnMode.Random)
        {
            StartSong();
        }
        else if (spawnMode == SpawnMode.Chart && loadedChart != null)
        {
            StartSong();
        }
    }

    private void Update()
    {
        if (!isGameStart || currentChart == null) return;

        // 피버 모드이거나 피버 종료 후 대기 중일 때는 노트 스폰 일시정지 (ResumeChartAfterFever에서 스킵 처리됨)
        if (!IsFeverTime && !isWaitingToResume)
        {
            double lastSpawnTime = -1;
            int lastSpawnLane = -1;

            // 멀티용 노트 생성 (동시 타격 노트들을 같은 프레임에 생성하기 위해 while문 사용)
            while (currentNoteIndex < currentChart.notes.Count)
            {
                NoteData nextNote = currentChart.notes[currentNoteIndex];
                double targetHitTime = exactStartTime + nextNote.time;
                if (AudioSettings.dspTime >= targetHitTime - noteDuration)
                {
                    // 중복 노트 스폰 방지 (물리 엔진 폭발 방지)
                    if (Mathf.Abs((float)(nextNote.time - lastSpawnTime)) < 0.01f && nextNote.lane == lastSpawnLane)
                    {
                        currentNoteIndex++;
                        continue;
                    }

                    SpawnIndividualNote(nextNote.lane, nextNote.span, targetHitTime, nextNote.type, currentNoteIndex);
                    lastSpawnTime = nextNote.time;
                    lastSpawnLane = nextNote.lane;
                    currentNoteIndex++;
                }
                else
                {
                    break;
                }
            }
        }

        if (Time.timeScale == 0) return;
        double currentTime = AudioSettings.dspTime;

        if (IsFeverTime) { }
        else if (spawnMode == SpawnMode.Random && !isWaitingToResume && !isGameCleared)
        {
            if (currentTime >= nextBeatTime)
            {
                GeneratePattern();
                nextBeatTime += secondsPerBeat;
                OnBeat?.Invoke();
            }
        }
        // ← Chart 부분 통째로 삭제!

        HandleSyncAdjustment();
    }

    private void HandleSyncAdjustment()
    {
        // Debug/Testing: Adjust sync offset in real-time
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            RhythmConfig.Instance.GlobalSyncOffset -= 0.005f;
            Debug.Log($"Sync Offset: {RhythmConfig.Instance.GlobalSyncOffset:F3}s");
        }
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            RhythmConfig.Instance.GlobalSyncOffset += 0.005f;
            Debug.Log($"Sync Offset: {RhythmConfig.Instance.GlobalSyncOffset:F3}s");
        }
    }

    private void HandleChartUpdate(double relativeTime)
    {
        // 1. Beat Event Logic
        double currentTime = AudioSettings.dspTime;
        if (currentTime >= nextBeatTime)
        {
            nextBeatTime += secondsPerBeat;
            OnBeat?.Invoke();
        }

        if (IsFeverTime) return; // 피버 연출 중에는 노트 스폰 중지

        // 2. Note Spawning Logic
        while (nextNoteIndex < loadedChart.notes.Count)
        {
            NoteData nextNote = loadedChart.notes[nextNoteIndex];

            if (relativeTime >= nextNote.time - noteDuration)
            {
                SpawnIndividualNote(nextNote.lane, nextNote.span, songStartTime + nextNote.time, nextNote.type);
                nextNoteIndex++;
            }
            else
            {
                break;
            }
        }
    }

    private void UpdateFeverUI()
    {
        if (feverGaugeController == null) return;

        if (currentFeverState == FeverState.None)
        {
            // 평상시에는 피버 게이지 충전량 표시
            feverGaugeController.UpdateFeverGauge(currentFeverGauge, MaxFeverGauge);
        }
    }

    private void HandleFeverAttack(int laneIndex)
    {
        if (currentFeverState != FeverState.Mashing) return;

        currentMashCount++;
        currentMashFloat += 1f; // For float-based logic
        SpawnHitEffect(Judgment.Perfect, laneIndex);

        // 연타 피드백 (UI)
        JudgmentUIController.Instance?.DisplayJudgment(laneIndex, Judgment.Perfect, true);
        UpdateFeverUI();
    }

    private System.Collections.IEnumerator FeverSequenceCoroutine()
    {
        bool isLaserDuel = feverSuccessCount >= 2;

        Vector3 originalCamPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        Quaternion originalCamRot = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;

        if (isLaserDuel && laserDuelCameraTarget != null && Camera.main != null)
        {
            StartCoroutine(MoveCameraCoroutine(laserDuelCameraTarget.position, laserDuelCameraTarget.rotation, cameraTransitionDuration));
        }

        // 1. Enter Animation (보스 쓰러짐 또는 레이저 준비)
        currentFeverState = FeverState.EnterAnimation;
        if (backgroundBossAnimator != null) backgroundBossAnimator.SetTrigger(isLaserDuel ? "Attack01" : "FallDown");
        yield return new WaitForSeconds(enterAnimDuration);

        // 2. Mashing Phase (제한시간 연타)
        currentFeverState = FeverState.Mashing;

        float currentTargetMashCount = isLaserDuel ? laserDuelRequiredMashCount : requiredMashCount;
        float currentPhaseDuration = isLaserDuel ? laserDuelDuration : mashingDuration;

        currentMashCount = isLaserDuel ? Mathf.RoundToInt(currentTargetMashCount * 0.5f) : 0;
        currentMashFloat = currentMashCount;

        // 제한 시간 동안 게이지 감소 연출 시작
        if (feverGaugeController != null) feverGaugeController.StartCountdown(currentPhaseDuration);

        GameObject pLaser = null;
        GameObject bLaser = null;
        GameObject clash = null;

        if (isLaserDuel)
        {
            if (playerLaserEffect != null && playerLaserSpawnPoint != null)
                pLaser = Instantiate(playerLaserEffect, playerLaserSpawnPoint.position, playerLaserSpawnPoint.rotation);
            if (bossLaserEffect != null && bossLaserSpawnPoint != null)
                bLaser = Instantiate(bossLaserEffect, bossLaserSpawnPoint.position, bossLaserSpawnPoint.rotation);
            if (laserClashEffect != null)
            {
                clash = Instantiate(laserClashEffect, Vector3.zero, laserClashEffect.transform.rotation);
                // 격돌 이펙트(SoundOrb 등)가 레이저 메시에 파묻히지 않도록 렌더링 순위를 최상단으로 강제
                Renderer[] renderers = clash.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.sortingOrder = 32000;
            }
        }

        float elapsedMashingTime = 0f;
        while (elapsedMashingTime < currentPhaseDuration)
        {
            if (isLaserDuel)
            {
                currentMashFloat -= bossPushSpeed * Time.deltaTime;
                currentMashFloat = Mathf.Clamp(currentMashFloat, 0f, currentTargetMashCount);
                currentMashCount = Mathf.RoundToInt(currentMashFloat);

                if (playerLaserSpawnPoint != null && bossLaserSpawnPoint != null)
                {
                    float progressRatio = currentMashFloat / currentTargetMashCount;
                    Vector3 clashPos = Vector3.Lerp(playerLaserSpawnPoint.position, bossLaserSpawnPoint.position, progressRatio);

                    bool hidePlayerLaser = progressRatio <= playerLaserHideThreshold;
                    bool hideBossLaser = progressRatio >= bossLaserHideThreshold;
                    bool hideClash = hidePlayerLaser || hideBossLaser;

                    if (clash != null)
                    {
                        if (clash.activeSelf == hideClash) clash.SetActive(!hideClash);
                        clash.transform.position = clashPos;
                    }

                    // 레이저 방향 및 길이 업데이트
                    if (pLaser != null)
                    {
                        if (pLaser.activeSelf == hidePlayerLaser) pLaser.SetActive(!hidePlayerLaser);

                        if (!hidePlayerLaser)
                        {
                            Vector3 pDir = clashPos - playerLaserSpawnPoint.position;
                            if (pDir != Vector3.zero)
                            {
                                pLaser.transform.position = playerLaserSpawnPoint.position;
                                pLaser.transform.rotation = Quaternion.FromToRotation(laserPrefabAxis, pDir);
                                if (stretchLasers)
                                {
                                    Vector3 scale = pLaser.transform.localScale;
                                    float targetLength = pDir.magnitude * playerLaserMultiplier;
                                    if (laserPrefabAxis == Vector3.up) scale.y = targetLength;
                                    else if (laserPrefabAxis == Vector3.forward) scale.z = targetLength;
                                    else if (laserPrefabAxis == Vector3.right) scale.x = targetLength;
                                    pLaser.transform.localScale = scale;
                                }
                            }
                        }
                    }

                    if (bLaser != null)
                    {
                        if (bLaser.activeSelf == hideBossLaser) bLaser.SetActive(!hideBossLaser);

                        if (!hideBossLaser)
                        {
                            Vector3 bDir = clashPos - bossLaserSpawnPoint.position;
                            if (bDir != Vector3.zero)
                            {
                                bLaser.transform.position = bossLaserSpawnPoint.position;
                                bLaser.transform.rotation = Quaternion.FromToRotation(laserPrefabAxis, bDir);
                                if (stretchLasers)
                                {
                                    Vector3 scale = bLaser.transform.localScale;
                                    float targetLength = bDir.magnitude * bossLaserMultiplier;
                                    if (laserPrefabAxis == Vector3.up) scale.y = targetLength;
                                    else if (laserPrefabAxis == Vector3.forward) scale.z = targetLength;
                                    else if (laserPrefabAxis == Vector3.right) scale.x = targetLength;
                                    bLaser.transform.localScale = scale;
                                }
                            }
                        }
                    }
                }
            }

            if (currentMashCount >= currentTargetMashCount)
            {
                break; // 목표 달성 시 즉시 루프 탈출 (조기 종료)
            }

            elapsedMashingTime += Time.deltaTime;
            yield return null;
        }

        // 3. Exit Animation (결과 판정 및 대미지)
        currentFeverState = FeverState.ExitAnimation;

        // 연타 판정 종료 직후 타이머(게이지 감소) 강제 중단 및 초기화
        if (feverGaugeController != null) feverGaugeController.ResetGauge();

        if (pLaser != null) Destroy(pLaser);
        if (bLaser != null) Destroy(bLaser);
        if (clash != null) Destroy(clash);

        bool isSuccess = currentMashCount >= currentTargetMashCount;

        if (isLaserDuel && isSuccess)
        {
            isGameCleared = true;
        }

        if (isSuccess)
        {
            feverSuccessCount++;
            Debug.Log($"Fever Success! Count: {feverSuccessCount}. Boss Takes Damage!");

            Vector3 spawnPosition = Vector3.zero;
            if (backgroundBossAnimator != null)
            {
                if (isLaserDuel) backgroundBossAnimator.SetTrigger("Die");
                else backgroundBossAnimator.SetTrigger("TakeDamage");

                spawnPosition = backgroundBossAnimator.transform.position;
            }

            if (feverSuccessCount == 1)
            {
                if (firstSuccessEffect != null) Instantiate(firstSuccessEffect, spawnPosition, firstSuccessEffect.transform.rotation);
            }
            else if (feverSuccessCount == 2)
            {
                if (secondSuccessEffect != null) Instantiate(secondSuccessEffect, spawnPosition, secondSuccessEffect.transform.rotation);
            }
            else if (feverSuccessCount >= 3)
            {
                // 3번째 성공 이후
                if (feverBossExplosion != null) Instantiate(feverBossExplosion, spawnPosition, feverBossExplosion.transform.rotation);
            }
        }
        else
        {
            Debug.Log("Fever Failed!");
            if (backgroundBossAnimator != null)
            {
                if (isLaserDuel) backgroundBossAnimator.SetTrigger("Victory");
                else backgroundBossAnimator.SetTrigger("Recover");
            }
        }

        // 카메라 위치 이동 (레이저 격돌 성공 시 클리어 타겟으로, 실패 시 원래 위치로)
        if (isLaserDuel && Camera.main != null)
        {
            if (isSuccess && laserDuelClearCameraTarget != null)
            {
                yield return StartCoroutine(MoveCameraCoroutine(laserDuelClearCameraTarget.position, laserDuelClearCameraTarget.rotation, cameraTransitionDuration));
            }
            else if (laserDuelCameraTarget != null) // 성공하지 않았거나 클리어 타겟이 없으면 원상복구
            {
                yield return StartCoroutine(MoveCameraCoroutine(originalCamPos, originalCamRot, cameraTransitionDuration));
            }
        }

        // 카메라 이동이 끝난 후 플레이어 애니메이션 재생
        if (isSuccess)
        {
            if (playerAnimators != null && playerAnimators.Length > 0)
            {
                string triggerName = UnityEngine.Random.Range(0, 2) == 0 ? "Victory 1" : "Victory 2";
                foreach (var anim in playerAnimators)
                {
                    if (anim != null) anim.SetTrigger(triggerName);
                }
            }
        }
        else
        {
            if (playerAnimators != null)
            {
                foreach (var anim in playerAnimators)
                {
                    if (anim != null) anim.SetTrigger("Hit");
                }
            }
        }

        yield return new WaitForSeconds(exitAnimDuration);

        // 4. Resume
        StartCoroutine(ResumeChartAfterFever());

        // 일반 노트 리스폰 및 상태 복구
        // 레이저 전투에서 실패하더라도 다음 피버 발동 시 다시 레이저 전투를 재도전할 수 있도록 카운트를 초기화하지 않음
        if (isLaserDuel && !isGameCleared)
        {
            Debug.Log("Laser Duel Failed, returning to regular note spawn (Will retry laser duel on next fever)");
        }

        SetFeverState(false);
    }

    private System.Collections.IEnumerator MoveCameraCoroutine(Vector3 targetPos, Quaternion targetRot, float duration)
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

    private System.Collections.IEnumerator ResumeChartAfterFever()
    {
        isWaitingToResume = true;
        currentFeverState = FeverState.None;

        // 게임 클리어가 아니라면(1, 2번째 피버 종료 시) 플레이어를 기본 상태(Idle/연주)로 강제 복구
        if (!isGameCleared && playerAnimators != null)
        {
            for (int i = 0; i < playerAnimators.Length; i++)
            {
                if (playerAnimators[i] != null)
                {
                    playerAnimators[i].Rebind();

                    // 루트 모션 등에 의해 변경된 트랜스폼을 원래 위치로 강제 초기화
                    if (playerInitialPositions != null && i < playerInitialPositions.Length)
                    {
                        playerAnimators[i].transform.localPosition = playerInitialPositions[i];
                        playerAnimators[i].transform.localRotation = playerInitialRotations[i];
                    }
                }
            }
        }

        // 피버 모드 토글 오브젝트 원상 복구
        if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(false);
        if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(true);

        UpdateFeverUI(); // 평상시 모드로 UI 강제 갱신
        OnFeverStateChanged?.Invoke(false); // UI 시스템에 피버 종료 알림

        yield return resumeDelay;

        double currentTime = AudioSettings.dspTime;

        float secPerBeat = 60f / bpm;
        float fourBeatsTime = 4.0f * secPerBeat;

        if (spawnMode == SpawnMode.Chart && currentChart != null)
        {
            // 피버 모드 직후 플레이어에게 반응 시간을 주기 위해
            // 화면에 꽉 차게 스폰되는(너무 가까운) 노트를 스킵하고, 4박자 이후의 노트부터 생성합니다.
            double relativeTime = currentTime - exactStartTime;
            double skipThresholdTime = relativeTime + fourBeatsTime;

            while (currentNoteIndex < currentChart.notes.Count && currentChart.notes[currentNoteIndex].time < skipThresholdTime)
            {
                currentNoteIndex++;
            }
        }
        else if (spawnMode == SpawnMode.Random)
        {
            // 랜덤 모드에서도 다음 생성될 노트가 정확히 4박자 전 위치에서 나타나도록 타이밍 조절
            nextBeatTime = currentTime - (noteDuration - fourBeatsTime);
        }

        isWaitingToResume = false;
    }

    private void GeneratePattern()
    {
        float rand = UnityEngine.Random.value;

        if (rand < 0.2f) SpawnChordPattern();       // Chord (20%)
        else if (rand < 0.35f) SpawnDoubleTapPattern(); // Double Tap (15%)
        else if (rand < 0.5f) SpawnDashPattern();    // Dash (15%)
        else SpawnIndividualNote(UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount), 1, nextBeatTime + noteDuration, NoteType.Normal); // Normal (50%)
    }

    private void SpawnIndividualNote(int startLane, int span, double hitTime, NoteType type, int chartNoteIndex = -1)
    {
        int totalLanes = RhythmConfig.Instance.LaneCount;

        startLane = Mathf.Clamp(startLane, 0, totalLanes - 1);

        // 트랙 바깥으로 노트가 나가지 않도록 span 제한
        if (startLane + span > totalLanes)
        {
            span = totalLanes - startLane;
        }

        if (span <= 1)
        {
            SpawnAndReturnIndividualNote(startLane, span, hitTime, type, chartNoteIndex);
            return;
        }

        NoteEnemy[] spawnedNotes = new NoteEnemy[span];

        // 하나의 커다란 노트 대신, 스케일이 1인 개별 노트를 span 개수만큼 생성
        for (int i = 0; i < span; i++)
        {
            int uniqueId = chartNoteIndex != -1 ? (chartNoteIndex * 10) + i : -1;
            spawnedNotes[i] = SpawnAndReturnIndividualNote(startLane + i, 1, hitTime, type, uniqueId);
        }

        // 같이치기 노트들 사이에 이펙트를 첫 번째 노트의 자식으로 추가
        if (chordConnectionEffect != null && spawnedNotes[0] != null)
        {
            float startX = (startLane - (totalLanes / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
            float endX = (startLane + span - 1 - (totalLanes / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;

            float centerX = (startX + endX) / 2f;
            // 부모(노트)의 회전(Y=180 등)에 의해 좌우가 반전되는 것을 막기 위해 월드 좌표 오프셋으로 전달
            Vector3 worldOffset = new Vector3(centerX - startX, 0, 0);

            GameObject effect = spawnedNotes[0].AddConnectionEffect(chordConnectionEffect, worldOffset, span);

            // 같이 치는 모든 노트가 연결 이펙트를 공유하도록 설정 (하나라도 맞으면 선이 끊어지도록)
            foreach (var note in spawnedNotes)
            {
                if (note != null) note.SetSharedConnectionEffect(effect);
            }
        }
    }

    private NoteEnemy SpawnAndReturnIndividualNote(int startLane, int span, double hitTime, NoteType type, int uniqueId = -1)
    {
        NoteEnemy enemy = null;

        if (notePools.TryGetValue(type, out var pool))
        {
            enemy = pool.Get();
            enemy.Initialize(pool, startLane, span, hitTime, noteDuration, beatsToArrive, type);
        }
        else
        {
            if (notePools.TryGetValue(NoteType.Normal, out var normalPool))
            {
                enemy = normalPool.Get();
                enemy.Initialize(normalPool, startLane, span, hitTime, noteDuration, beatsToArrive, type);
            }
        }

        if (enemy != null)
        {
            enemy.myNoteId = uniqueId != -1 ? uniqueId : _globalNoteId++; // 결정론적 ID 부여 (네트워크 동기화)
            // activeNotes.Add(enemy)는 ObjectPool의 actionOnGet에서 이미 호출되므로 중복 호출하지 않음
        }
        return enemy;
    }

    // 1. Chord Pattern (Multiple normal notes spawning simultaneously)
    private void SpawnChordPattern()
    {
        int totalLanes = RhythmConfig.Instance.LaneCount;
        int span = UnityEngine.Random.Range(2, totalLanes + 1); // Spans 2-4 lanes
        int startLane = UnityEngine.Random.Range(0, totalLanes - span + 1);

        SpawnIndividualNote(startLane, span, nextBeatTime + noteDuration, NoteType.Normal);
    }

    // 2. Double Tap Pattern (One note with health 2)
    private void SpawnDoubleTapPattern()
    {
        int lane = UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount);
        SpawnIndividualNote(lane, 1, nextBeatTime + noteDuration, NoteType.Double);
    }

    // 3. Dash Pattern (Pause then dash)
    private void SpawnDashPattern()
    {
        int lane = UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount);
        SpawnIndividualNote(lane, 1, nextBeatTime + noteDuration, NoteType.Dash);
    }



    public void OnInputLane0(InputAction.CallbackContext context) { if (context.performed) ExecuteInput(0); }
    public void OnInputLane1(InputAction.CallbackContext context) { if (context.performed) ExecuteInput(1); }
    public void OnInputLane2(InputAction.CallbackContext context) { if (context.performed) ExecuteInput(2); }
    public void OnInputLane3(InputAction.CallbackContext context) { if (context.performed) ExecuteInput(3); }


    private void ExecuteInput(int laneIndex)
    {
        if (laneIndex >= 0 && laneIndex < inputEffects.Length)
            inputEffects[laneIndex]?.PlayEffect();

        ProcessHitInput(laneIndex);
    }

    private void ProcessHitInput(int laneIndex)
    {
        if (IsFeverTime)
        {
            HandleFeverAttack(laneIndex);
            return;
        }

        // 1. [수정] GetNearestNote 함수 호출 대신 null로 초기화
        NoteEnemy closestNote = null;
        double minTimeOffset = double.MaxValue;

        // 2. 가장 가까운 노트 찾기 (주인님의 기존 로직)
        foreach (var note in activeNotes)
        {
            if (!note.IsOccupyingLane(laneIndex) || note.IsLaneAlreadyHit(laneIndex)) continue;

            double timeOffset = Math.Abs(AudioSettings.dspTime - note.TargetHitTime - RhythmConfig.Instance.GlobalSyncOffset);

            float thresholdMultiplier = (note.Type == NoteType.Double && note.HitsRemaining < 2) ? 2.0f : 1.0f;
            float maxThreshold = RhythmConfig.Instance.GoodThreshold * thresholdMultiplier;

            if (timeOffset <= maxThreshold && timeOffset < minTimeOffset)
            {
                minTimeOffset = timeOffset;
                closestNote = note;
            }
        }

        // 3. [수정] 찾은 노트 처리 (중복된 if문들을 하나로 통합)
        if (closestNote != null)
        {
            closestNote.MarkLaneHit(laneIndex);

            float thresholdMultiplier = (closestNote.Type == NoteType.Double && closestNote.HitsRemaining < 2) ? 2.0f : 1.0f;
            Judgment result = EvaluateJudgment(minTimeOffset, thresholdMultiplier);

            ApplyHitResult(result, laneIndex, closestNote);
            closestNote.OnHit();

            // 서버에 타격 보고 (멀티플레이 핵심) - 내 레인의 입력일 때만 전송하여 중복 패킷 방지
            if (laneIndex == myLaneIndex && RhythmPlayer.LocalInstance != null)
            {
                RhythmPlayer.LocalInstance.CmdHitNote(laneIndex, closestNote.myNoteId);
            }
        }
    }

    private Judgment EvaluateJudgment(double timeOffset, float multiplier = 1.0f)
    {
        if (timeOffset <= RhythmConfig.Instance.PerfectThreshold * multiplier) return Judgment.Perfect;
        if (timeOffset <= RhythmConfig.Instance.GreatThreshold * multiplier) return Judgment.Great;
        if (timeOffset <= RhythmConfig.Instance.GoodThreshold * multiplier) return Judgment.Good;
        return Judgment.Miss;
    }

    private void ApplyHitResult(Judgment result, int laneIndex, NoteEnemy note)
    {
        bool isFeverNote = note != null && note.Type == NoteType.Fever;

        if (result == Judgment.Miss)
        {
            // Reset combo only for normal notes or fever notes after fever ended
            if (!IsFeverTime && !isFeverNote)
            {
                ResetCombo();
                DecreaseFeverGauge();
                PlayPlayerHitAnimation();
            }
        }
        else
        {
            currentCombo++;
            maxCombo = Math.Max(maxCombo, currentCombo);

            // Increase gauge only when not in Fever and not hitting Fever-only notes
            if (!IsFeverTime && !isFeverNote && note != null && !note.HasContributedToFever)
            {
                note.HasContributedToFever = true; // Mark as contributed
                currentFeverGauge += gaugePerHit;
                if (currentFeverGauge >= MaxFeverGauge)
                {
                    currentFeverGauge = MaxFeverGauge;
                    // 진입 전 시각적으로 100%를 찍도록 강제 업데이트
                    if (feverGaugeController != null) feverGaugeController.UpdateFeverGauge(currentFeverGauge, MaxFeverGauge);

                    SetFeverState(true);
                    currentFeverGauge = 0f; // Reset after activation
                }
                else
                {
                    UpdateFeverUI();
                }
            }

            SpawnHitEffect(result, laneIndex); // Spawn effect
        }

        // Output judgment text per lane (FEVER if in Fever mode or hitting Fever note)
        JudgmentUIController.Instance?.DisplayJudgment(laneIndex, result, IsFeverTime || isFeverNote);

        OnNoteHit?.Invoke(result, currentCombo);
        Debug.Log($"Hit! [{result}] Combo: {currentCombo}");
    }

    private void SpawnHitEffect(Judgment result, int laneIndex)
    {
        IObjectPool<GameObject> targetPool = (result == Judgment.Perfect || result == Judgment.Great) ? poolPerfect : poolGood;
        GameObject effect = targetPool.Get();

        // Set position: Lane X, Judge Line Z
        float xPos = (laneIndex - (RhythmConfig.Instance.LaneCount / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
        effect.transform.position = new Vector3(xPos, 0.1f, RhythmConfig.Instance.JudgeLineZ);

        // Return to pool after delay (Default 1s)
        StartCoroutine(ReturnToPoolAfterDelay(effect, targetPool, 1.0f));
    }

    private System.Collections.IEnumerator ReturnToPoolAfterDelay(GameObject effect, IObjectPool<GameObject> pool, float delay)
    {
        yield return effectReturnDelay;
        pool.Release(effect);
    }

    public void ReportMiss(NoteEnemy note)
    {
        bool isFeverNote = note != null && note.Type == NoteType.Fever;
        if (IsFeverTime || isFeverNote) return; // Maintain combo during Fever or for Fever notes

        ResetCombo();
        DecreaseFeverGauge();
        PlayPlayerHitAnimation();

        // For giant notes, show MISS only for lanes not hit
        for (int i = note.StartLane; i < note.StartLane + note.LaneSpan; i++)
        {
            if (!note.IsLaneAlreadyHit(i))
            {
                JudgmentUIController.Instance?.DisplayJudgment(i, Judgment.Miss, IsFeverTime || isFeverNote);
            }
        }

        OnNoteHit?.Invoke(Judgment.Miss, currentCombo);
        Debug.Log("Missed!");
    }

    private void PlayPlayerHitAnimation()
    {
        if (playerAnimators != null)
        {
            foreach (var anim in playerAnimators)
            {
                if (anim != null) anim.SetTrigger("Hit");
            }
        }
    }
    private void ResetCombo()
    {
        currentCombo = 0;
    }

    private void DecreaseFeverGauge()
    {
        if (IsFeverTime) return;

        currentFeverGauge = Mathf.Max(0f, currentFeverGauge - gaugeLossOnMiss);
        UpdateFeverUI();
    }

    private void SetFeverState(bool active)
    {
        if (active)
        {
            // 피버 모드 진입 시 오브젝트 켜기/끄기
            if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(true);
            if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(false);

            // 1. 화면에 남은 일반 노트들 클리어
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                activeNotes[i].ReleaseToPool();
            }

            // 2. 새로운 피버 시퀀스 시작
            StartCoroutine(FeverSequenceCoroutine());
        }

        UpdateFeverUI();
        OnFeverStateChanged?.Invoke(active);
    }

    public void TriggerLaneInput(int laneIndex)
    {
        if (inputEffects != null && laneIndex < inputEffects.Length)
            inputEffects[laneIndex]?.PlayEffect();
        ExecuteInput(laneIndex);
    }

    private void OnGUI()
    {
        // 테스트용: 3번째 피버(레이저 격돌) 강제 진입
        if (GUI.Button(new Rect(10, 10, 150, 50), "Trigger 3rd Fever"))
        {
            if (!IsFeverTime)
            {
                feverSuccessCount = 2; // 다음 피버가 무조건 레이저 전투(3번째)가 되도록 설정
                currentFeverGauge = MaxFeverGauge;
                SetFeverState(true);
            }
        }
    }
    public void OnInputSpacebar(InputAction.CallbackContext context)
    {
        // 내 컴퓨터에서 내 레인만 칩니다!
        if (context.performed) ExecuteInput(myLaneIndex);
    }   
}