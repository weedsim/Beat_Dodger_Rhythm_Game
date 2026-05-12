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
    [SerializeField] private Animator backgroundBossAnimator; // 諛곌꼍??嫄곕? 蹂댁뒪 ?좊땲硫붿씠??    [SerializeField] private float enterAnimDuration = 2.0f;
    [SerializeField] private float mashingDuration = 5.0f;
    [SerializeField] private float exitAnimDuration = 2.0f;
    [SerializeField] private int requiredMashCount = 50;
    [SerializeField] private GameObject feverBossExplosion;
    
    [Header("Fever Toggle Objects")]
    [Tooltip("?쇰쾭 紐⑤뱶 吏꾩엯 ??耳쒖쭏 ?ㅻ툕?앺듃??(?쇰쾭 醫낅즺 ???ㅼ떆 爰쇱쭚)")]
    [SerializeField] private List<GameObject> feverEnableObjects = new List<GameObject>();
    [Tooltip("?쇰쾭 紐⑤뱶 吏꾩엯 ??爰쇱쭏 ?ㅻ툕?앺듃??(?쇰쾭 醫낅즺 ???ㅼ떆 耳쒖쭚)")]
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
            loadedChart.SortNotes(); // 媛뺤젣濡??쒓컙???뺣젹
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
                // 1 ?먮뒗 2瑜??쒕뜡?섍쾶 ?좏깮?섏뿬 ?몃━嫄?諛쒕룞
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
            // Mashing ?④퀎?먯꽌 ?⑥? ?쒓컙??UI???쒖떆?섍굅???곗텧???낅뜲?댄듃?섎뒗 濡쒖쭅???꾩슂?섎떎硫??ш린??異붽?
        }
        else if (spawnMode == SpawnMode.Random)
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

        if (IsFeverTime) return; // ?쇰쾭 ?곗텧 以묒뿉???명듃 ?ㅽ룿 以묒?

        // 2. Note Spawning Logic
        if (nextNoteIndex < loadedChart.notes.Count)
        {
            NoteData nextNote = loadedChart.notes[nextNoteIndex];
            
            if (relativeTime >= nextNote.time - noteDuration)
            {
                SpawnIndividualNote(nextNote.lane, nextNote.span, songStartTime + nextNote.time, nextNote.type);
                nextNoteIndex++;
            }
        }
    }

    private void UpdateFeverUI()
    {
        if (feverGaugeController == null) return;

        if (currentFeverState == FeverState.None)
        {
            // ?됱긽?쒖뿉???쇰쾭 寃뚯씠吏 異⑹쟾???쒖떆
            feverGaugeController.UpdateFeverGauge(currentFeverGauge, MaxFeverGauge);
        }
    }

    private void HandleFeverAttack(int laneIndex)
    {
        if (currentFeverState != FeverState.Mashing) return;

        currentMashCount++;
        SpawnHitEffect(Judgment.Perfect, laneIndex);
        
        // ?고? ?쇰뱶諛?(UI)
        JudgmentUIController.Instance?.DisplayJudgment(laneIndex, Judgment.Perfect, true);
        UpdateFeverUI();
    }

    private System.Collections.IEnumerator FeverSequenceCoroutine()
    {
        // 1. Enter Animation (蹂댁뒪 ?곕윭吏?
        currentFeverState = FeverState.EnterAnimation;
        if (backgroundBossAnimator != null) backgroundBossAnimator.SetTrigger("FallDown"); // TODO: ?ㅼ젣 ?뚮씪誘명꽣紐낆뿉 留욊쾶 ?섏젙 ?꾩슂
        yield return new WaitForSeconds(enterAnimDuration);

        // 2. Mashing Phase (?쒗븳?쒓컙 ?고?)
        currentFeverState = FeverState.Mashing;
        currentMashCount = 0;
        
        // ?쒗븳 ?쒓컙 ?숈븞 寃뚯씠吏 媛먯냼 ?곗텧 ?쒖옉
        if (feverGaugeController != null) feverGaugeController.StartCountdown(mashingDuration);
        
        float elapsedMashingTime = 0f;
        while (elapsedMashingTime < mashingDuration)
        {
            if (currentMashCount >= requiredMashCount)
            {
                break; // 紐⑺몴 ?ъ꽦 ??利됱떆 猷⑦봽 ?덉텧 (議곌린 醫낅즺)
            }
            elapsedMashingTime += Time.deltaTime;
            yield return null;
        }

        // 3. Exit Animation (寃곌낵 ?먯젙 諛??誘몄?)
        currentFeverState = FeverState.ExitAnimation;
        
        // ?고? ?먯젙 醫낅즺 吏곹썑 ?癒?寃뚯씠吏 媛먯냼) 媛뺤젣 以묐떒 諛?珥덇린??        if (feverGaugeController != null) feverGaugeController.ResetGauge();

        if (currentMashCount >= requiredMashCount)
        {
            Debug.Log("Fever Success! Boss Takes Damage!");
            if (backgroundBossAnimator != null) backgroundBossAnimator.SetTrigger("TakeDamage");
            if (feverBossExplosion != null) Instantiate(feverBossExplosion, Vector3.zero, Quaternion.identity); // TODO: 蹂댁뒪 ?꾩튂 吏???꾩슂
        }
        else
        {
            Debug.Log("Fever Failed!");
            if (backgroundBossAnimator != null) backgroundBossAnimator.SetTrigger("Recover");
        }
        
        yield return new WaitForSeconds(exitAnimDuration);

        // 4. Resume
        StartCoroutine(ResumeChartAfterFever());
    }

    private System.Collections.IEnumerator ResumeChartAfterFever()
    {
        isWaitingToResume = true;
        currentFeverState = FeverState.None;
        
        // ?쇰쾭 紐⑤뱶 ?좉? ?ㅻ툕?앺듃 ?먯긽 蹂듦뎄
        if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(false);
        if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(true);
        
        UpdateFeverUI(); // ?됱긽??紐⑤뱶濡?UI 媛뺤젣 媛깆떊
        OnFeverStateChanged?.Invoke(false); // UI ?쒖뒪?쒖뿉 ?쇰쾭 醫낅즺 ?뚮┝
        
        yield return resumeDelay;
        
        double currentTime = AudioSettings.dspTime;

        float secPerBeat = 60f / bpm;
        float fourBeatsTime = 4.0f * secPerBeat;

        if (spawnMode == SpawnMode.Chart && loadedChart != null)
        {
            // ?몄뒪?숉꽣??Beats To Arrive(?? 8)? 臾닿??섍쾶, ??긽 ?뺥솗??'4諛뺤옄' ?꾩튂(4踰덉㎏ ????먯꽌 ?앹꽦?섎룄濡?4諛뺤옄 遺꾨웾留??ㅽ궢?⑸땲??
            double relativeTime = mainAudioSource != null ? mainAudioSource.time : currentTime - songStartTime;
            double skipThresholdTime = relativeTime + fourBeatsTime;
            
            while (nextNoteIndex < loadedChart.notes.Count && loadedChart.notes[nextNoteIndex].time < skipThresholdTime)
            {
                nextNoteIndex++;
            }
        }
        else if (spawnMode == SpawnMode.Random)
        {
            // ?쒕뜡 紐⑤뱶?먯꽌???ㅼ쓬 ?앹꽦???명듃媛 ?뺥솗??4諛뺤옄 ???꾩튂?먯꽌 ?섑??섎룄濡???대컢 議곗젅
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
                    // 吏꾩엯 ???쒓컖?곸쑝濡?100%瑜?李띾룄濡?媛뺤젣 ?낅뜲?댄듃
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
            // ?쇰쾭 紐⑤뱶 吏꾩엯 ???ㅻ툕?앺듃 耳쒓린/?꾧린
            if (feverEnableObjects != null) foreach (var obj in feverEnableObjects) if (obj != null) obj.SetActive(true);
            if (feverDisableObjects != null) foreach (var obj in feverDisableObjects) if (obj != null) obj.SetActive(false);

            // 1. ?붾㈃???⑥? ?쇰컲 ?명듃???대━??            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                activeNotes[i].ReleaseToPool();
            }

            // 2. ?덈줈???쇰쾭 ?쒗???쒖옉
            StartCoroutine(FeverSequenceCoroutine());
        }

        UpdateFeverUI();
        OnFeverStateChanged?.Invoke(active);
    }

    private void OnGUI()
    {
        // ?뚯뒪?몄슜: Mashing ?섏씠利덉씪 ???붾㈃ 以묒븰 ?곷떒???고? ?잛닔 異쒕젰
        if (currentFeverState == FeverState.Mashing)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 60;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.yellow;
            style.alignment = TextAnchor.MiddleCenter;

            Rect rect = new Rect(0, Screen.height * 0.3f, Screen.width, 100);
            GUI.Label(rect, $"MASH: {currentMashCount} / {requiredMashCount}", style);
        }
    }
}
