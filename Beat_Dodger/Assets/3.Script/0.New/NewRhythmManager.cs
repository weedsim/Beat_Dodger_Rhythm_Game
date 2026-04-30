using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.InputSystem;

public class NewRhythmManager : MonoBehaviour
{
    public static NewRhythmManager Instance { get; private set; }

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
    [SerializeField] private float enterAnimDuration = 2.0f;
    [SerializeField] private float mashingDuration = 5.0f;
    [SerializeField] private float exitAnimDuration = 2.0f;
    [SerializeField] private int requiredMashCount = 50;
    [SerializeField] private GameObject feverBossExplosion;
    
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
    [SerializeField] private LaneInputEffect[] inputEffects;
    [SerializeField] private GameObject hitEffectPerfect; // For Perfect & Great
    [SerializeField] private GameObject hitEffectGood;    // For Good

    private Dictionary<NoteType, IObjectPool<NoteEnemy>> notePools = new Dictionary<NoteType, IObjectPool<NoteEnemy>>();
    private IObjectPool<GameObject> poolPerfect;
    private IObjectPool<GameObject> poolGood;
    private readonly List<NoteEnemy> activeNotes = new List<NoteEnemy>();

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

    // Events (UI and System integration)
    public static event Action OnBeat;
    public event Action<Judgment, int> OnNoteHit; 
    public static event Action<bool> OnFeverStateChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

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
        songStartTime = AudioSettings.dspTime + startDelay;
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
            createFunc: () => {
                GameObject instance = Instantiate(prefab);
                return instance.TryGetComponent<NoteEnemy>(out NoteEnemy note) ? note : instance.AddComponent<NoteEnemy>();
            },
            actionOnGet: (note) => {
                note.gameObject.SetActive(true);
                activeNotes.Add(note);
            },
            actionOnRelease: (note) => {
                note.gameObject.SetActive(false);
                activeNotes.Remove(note);
            },
            actionOnDestroy: (note) => Destroy(note.gameObject),
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 30
        );
        
        notePools[type] = pool;
    }

    private void OnDestroyNoteFromPool(NoteEnemy noteEnemy) { Destroy(noteEnemy.gameObject); }

    private void Start()
    {
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
        if (Time.timeScale == 0) return;

        double currentTime = AudioSettings.dspTime;

        if (IsFeverTime)
        {
            // Mashing 단계에서 남은 시간을 UI에 표시하거나 연출을 업데이트하는 로직이 필요하다면 여기에 추가
        }
        else if (spawnMode == SpawnMode.Random && !isWaitingToResume)
        {
            if (currentTime >= nextBeatTime)
            {
                GeneratePattern();
                nextBeatTime += secondsPerBeat;
                OnBeat?.Invoke();
            }
        }
        else if (spawnMode == SpawnMode.Chart && isSongPlaying)
        {
            // Use mainAudioSource.time for perfect sync with audio
            double relativeTime = mainAudioSource != null ? mainAudioSource.time : AudioSettings.dspTime - songStartTime;
            if (!isWaitingToResume) HandleChartUpdate(relativeTime);
        }

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
        if (backgroundBossAnimator != null) backgroundBossAnimator.SetTrigger(isLaserDuel ? "LaserReady" : "FallDown"); // TODO: 실제 파라미터명에 맞게 수정 필요
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
                clash = Instantiate(laserClashEffect, Vector3.zero, Quaternion.identity);
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
                    
                    if (clash != null)
                    {
                        clash.transform.position = clashPos;
                    }
                    
                    // 레이저 방향 및 길이 업데이트
                    if (pLaser != null)
                    {
                        bool hidePlayerLaser = progressRatio <= playerLaserHideThreshold;
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
                        bool hideBossLaser = progressRatio >= bossLaserHideThreshold;
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
        
        if (isLaserDuel && laserDuelCameraTarget != null && Camera.main != null)
        {
            StartCoroutine(MoveCameraCoroutine(originalCamPos, originalCamRot, cameraTransitionDuration));
        }
        
        // 연타 판정 종료 직후 타이머(게이지 감소) 강제 중단 및 초기화
        if (feverGaugeController != null) feverGaugeController.ResetGauge();
        
        if (pLaser != null) Destroy(pLaser);
        if (bLaser != null) Destroy(bLaser);
        if (clash != null) Destroy(clash);

        if (currentMashCount >= currentTargetMashCount)
        {
            feverSuccessCount++;
            Debug.Log($"Fever Success! Count: {feverSuccessCount}. Boss Takes Damage!");
            
            Vector3 spawnPosition = Vector3.zero;
            if (backgroundBossAnimator != null)
            {
                backgroundBossAnimator.SetTrigger("TakeDamage");
                spawnPosition = backgroundBossAnimator.transform.position;
            }
            
            if (feverSuccessCount == 1)
            {
                if (firstSuccessEffect != null) Instantiate(firstSuccessEffect, spawnPosition, Quaternion.identity);
            }
            else if (feverSuccessCount == 2)
            {
                if (secondSuccessEffect != null) Instantiate(secondSuccessEffect, spawnPosition, Quaternion.identity);
            }
            else if (feverSuccessCount >= 3)
            {
                // 3번째 성공 이후
                if (feverBossExplosion != null) Instantiate(feverBossExplosion, spawnPosition, Quaternion.identity);
            }
        }
        else
        {
            Debug.Log("Fever Failed!");
            if (backgroundBossAnimator != null) backgroundBossAnimator.SetTrigger("Recover");
        }
        
        yield return new WaitForSeconds(exitAnimDuration);

        // 4. Resume
        StartCoroutine(ResumeChartAfterFever());

        // 일반 노트 리스폰 및 상태 복구
        if (isLaserDuel)
        {
            feverSuccessCount = 0;
            Debug.Log("Laser Duel Phase Ended, returning to regular note spawn");
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
        
        // 피버 모드 토글 오브젝트 원상 복구
        if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(false);
        if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(true);
        
        UpdateFeverUI(); // 평상시 모드로 UI 강제 갱신
        OnFeverStateChanged?.Invoke(false); // UI 시스템에 피버 종료 알림
        
        yield return resumeDelay;
        
        double currentTime = AudioSettings.dspTime;

        float secPerBeat = 60f / bpm;
        float fourBeatsTime = 4.0f * secPerBeat;

        if (spawnMode == SpawnMode.Chart && loadedChart != null)
        {
            // 인스펙터의 Beats To Arrive(예: 8)와 무관하게, 항상 정확히 '4박자' 위치(4번째 타일)에서 생성되도록 4박자 분량만 스킵합니다.
            double relativeTime = mainAudioSource != null ? mainAudioSource.time : currentTime - songStartTime;
            double skipThresholdTime = relativeTime + fourBeatsTime;
            
            while (nextNoteIndex < loadedChart.notes.Count && loadedChart.notes[nextNoteIndex].time < skipThresholdTime)
            {
                nextNoteIndex++;
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

    // Common logic for spawning individual or giant notes
    private void SpawnIndividualNote(int startLane, int span, double hitTime, NoteType type)
    {
        if (notePools.TryGetValue(type, out var pool))
        {
            NoteEnemy enemy = pool.Get();
            enemy.Initialize(pool, startLane, span, hitTime, noteDuration, beatsToArrive, type);
        }
        else
        {
            // Fallback to normal pool if specific pool not found
            if (notePools.TryGetValue(NoteType.Normal, out var normalPool))
            {
                NoteEnemy enemy = normalPool.Get();
                enemy.Initialize(normalPool, startLane, span, hitTime, noteDuration, beatsToArrive, type);
            }
        }
    }

    // 1. Chord Pattern (One giant note spanning multiple lanes)
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

        NoteEnemy closestNote = null;
        double minTimeOffset = double.MaxValue;

        // Find the closest note (exact hit timing)
        foreach (var note in activeNotes)
        {
            // Check if note occupies the lane and hasn't been hit in this lane yet
            if (!note.IsOccupyingLane(laneIndex) || note.IsLaneAlreadyHit(laneIndex)) continue;

            // Apply Global Sync Offset (dspTime - targetTime - offset)
            double timeOffset = Math.Abs(AudioSettings.dspTime - note.TargetHitTime - RhythmConfig.Instance.GlobalSyncOffset);
            
            // Expand judgment window for Double Tap notes if hit once
            float thresholdMultiplier = (note.Type == NoteType.Double && note.HitsRemaining < 2) ? 2.0f : 1.0f;
            float maxThreshold = RhythmConfig.Instance.GoodThreshold * thresholdMultiplier;

            if (timeOffset <= maxThreshold && timeOffset < minTimeOffset)
            {
                minTimeOffset = timeOffset;
                closestNote = note;
            }
        }

        if (closestNote != null)
        {
            // Record hit for this lane
            closestNote.MarkLaneHit(laneIndex);

            // Determine judgment rank with multiplier
            float thresholdMultiplier = (closestNote.Type == NoteType.Double && closestNote.HitsRemaining < 2) ? 2.0f : 1.0f;
            Judgment result = EvaluateJudgment(minTimeOffset, thresholdMultiplier);
            
            ApplyHitResult(result, laneIndex, closestNote);
            closestNote.OnHit();
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

    private void OnGUI()
    {
        // 테스트용: Mashing 페이즈일 때 화면 중앙 상단에 연타 횟수 출력
        if (currentFeverState == FeverState.Mashing)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 60;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.yellow;
            style.alignment = TextAnchor.MiddleCenter;

            Rect rect = new Rect(0, Screen.height * 0.3f, Screen.width, 100);
            int displayTarget = (feverSuccessCount >= 2) ? laserDuelRequiredMashCount : requiredMashCount;
            GUI.Label(rect, $"MASH: {currentMashCount} / {displayTarget}", style);
        }

        // 테스트 버튼: 레이저 격돌 바로 시작
        if (GUI.Button(new Rect(10, 10, 200, 50), "Test Laser Duel"))
        {
            StopAllCoroutines();
            feverSuccessCount = 2; // 3번째 피버 강제 지정
            SetFeverState(true);
        }
    }
}