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
    [SerializeField] private float feverDuration = 5.0f;
    [SerializeField] private float gaugePerHit = 10f; // Fever after 10 hits (Gauge 100)
    [SerializeField] private float gaugeLossOnMiss = 5f; // Gauge loss on Miss
    [SerializeField] private UnityEngine.UI.Slider feverSlider; // Fever Gauge / Boss HP
    [SerializeField] private int feverAttackRequirement = 30; // Number of hits to kill boss
    [SerializeField] private GameObject feverBossPrefab;    // Unique Boss Prefab
    [SerializeField] private GameObject feverBossExplosion; // Unique Explosion Effect
    
    public enum SpawnMode { Random, Chart }
    [Header("Spawn Mode")]
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Random;
    [SerializeField] private RhythmChart chartAsset;
    [SerializeField] private AudioSource mainAudioSource;

    private bool isFeverTime;
    private float feverTimer;
    private int currentFeverHits;
    private NoteEnemy currentFeverBoss;
    private float currentFeverGauge;
    private bool isWaitingToResume;
    
    private const float MaxFeverGauge = 100f;
    private const float ResumeDelaySeconds = 1.5f;
    private const float EffectReturnDelaySeconds = 1.0f;

    private readonly WaitForSeconds resumeDelay = new WaitForSeconds(ResumeDelaySeconds);
    private readonly WaitForSeconds effectReturnDelay = new WaitForSeconds(EffectReturnDelaySeconds);

    public bool IsFeverTime => isFeverTime;
    public float FeverProgress => isFeverTime ? (feverTimer / feverDuration) : Mathf.Clamp01(currentFeverGauge / MaxFeverGauge);

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

        if (isFeverTime)
        {
            HandleFeverUpdate(Time.deltaTime);
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

        if (isFeverTime) return; // Don't spawn chart notes during Fever

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

    private void HandleFeverUpdate(float dt)
    {
        // UI updates are now event-driven or state-driven to reduce overhead
    }

    private void UpdateFeverUI()
    {
        if (feverSlider != null)
        {
            // During Fever, show Boss HP (starts full, decreases)
            // Otherwise show Fever Gauge progress (starts empty, increases)
            feverSlider.value = isFeverTime 
                ? 1.0f - ((float)currentFeverHits / feverAttackRequirement) 
                : currentFeverGauge / MaxFeverGauge;
        }
    }

    private void HandleFeverAttack(int laneIndex)
    {
        currentFeverHits++;
        // Combo no longer builds up during Fever Boss encounter
        
        SpawnHitEffect(Judgment.Perfect, laneIndex);
        
        // Notify UI for feedback, but pass the existing static combo
        OnNoteHit?.Invoke(Judgment.Perfect, currentCombo);
        JudgmentUIController.Instance?.DisplayJudgment(laneIndex, Judgment.Perfect, true);

        if (currentFeverHits >= feverAttackRequirement)
        {
            FinishFeverBoss();
        }
        UpdateFeverUI();
    }

    private void FinishFeverBoss()
    {
        if (currentFeverBoss != null)
        {
            // Play big explosion effect at boss's current world position
            if (feverBossExplosion != null)
            {
                Instantiate(feverBossExplosion, currentFeverBoss.transform.position, Quaternion.identity);
            }
            
            CleanupFeverBoss();
        }
        
        StartCoroutine(ResumeChartAfterFever());
    }

    private void CleanupFeverBoss()
    {
        if (currentFeverBoss == null) return;

        activeNotes.Remove(currentFeverBoss);
        // Note: Boss is not pooled, so we destroy it
        Destroy(currentFeverBoss.gameObject);
        currentFeverBoss = null;
    }

    private System.Collections.IEnumerator ResumeChartAfterFever()
    {
        isWaitingToResume = true;
        SetFeverState(false);
        
        yield return resumeDelay;
        
        // Skip notes that passed during Fever to keep sync
        double relativeTime = mainAudioSource != null ? mainAudioSource.time : AudioSettings.dspTime - songStartTime;
        while (nextNoteIndex < loadedChart.notes.Count && loadedChart.notes[nextNoteIndex].time < relativeTime)
        {
            nextNoteIndex++;
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
        if (isFeverTime)
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
            if (!isFeverTime && !isFeverNote)
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
            if (!isFeverTime && !isFeverNote && note != null && !note.HasContributedToFever)
            {
                note.HasContributedToFever = true; // Mark as contributed
                currentFeverGauge += gaugePerHit;
                if (currentFeverGauge >= MaxFeverGauge)
                {
                    currentFeverGauge = MaxFeverGauge;
                    SetFeverState(true);
                    currentFeverGauge = 0f; // Reset after activation
                }
                UpdateFeverUI();
            }

            SpawnHitEffect(result, laneIndex); // Spawn effect
        }

        // Output judgment text per lane (FEVER if in Fever mode or hitting Fever note)
        JudgmentUIController.Instance?.DisplayJudgment(laneIndex, result, isFeverTime || isFeverNote);

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
        if (note == currentFeverBoss)
        {
            HandleBossMiss();
            return;
        }

        bool isFeverNote = note != null && note.Type == NoteType.Fever;
        if (isFeverTime || isFeverNote) return; // Maintain combo during Fever or for Fever notes

        ResetCombo();
        DecreaseFeverGauge();
        
        // For giant notes, show MISS only for lanes not hit
        for (int i = note.StartLane; i < note.StartLane + note.LaneSpan; i++)
        {
            if (!note.IsLaneAlreadyHit(i))
            {
                JudgmentUIController.Instance?.DisplayJudgment(i, Judgment.Miss, isFeverTime || isFeverNote);
            }
        }

        OnNoteHit?.Invoke(Judgment.Miss, currentCombo);
        Debug.Log("Missed!");
    }

    private void HandleBossMiss()
    {
        CleanupFeverBoss();
        ResetCombo();
        
        // Show MISS on all lanes for the boss
        for (int i = 0; i < RhythmConfig.Instance.LaneCount; i++)
        {
            JudgmentUIController.Instance?.DisplayJudgment(i, Judgment.Miss, false);
        }

        OnNoteHit?.Invoke(Judgment.Miss, currentCombo);
        StartCoroutine(ResumeChartAfterFever());
    }

    private void ResetCombo()
    {
        currentCombo = 0;
    }

    private void DecreaseFeverGauge()
    {
        if (isFeverTime) return;

        currentFeverGauge = Mathf.Max(0f, currentFeverGauge - gaugeLossOnMiss);
        UpdateFeverUI();
    }

    private void SetFeverState(bool active)
    {
        isFeverTime = active;
        if (active)
        {
            // 1. Clear all existing notes without allocation
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                activeNotes[i].ReleaseToPool();
            }

            // 2. Spawn Giant Boss from unique prefab
            currentFeverHits = 0;
            const float BossWaitBeats = 4.0f;
            double bossHitTime = AudioSettings.dspTime + (secondsPerBeat * BossWaitBeats);
            
            GameObject bossGo = Instantiate(feverBossPrefab);
            currentFeverBoss = bossGo.GetComponentInChildren<NoteEnemy>();
            
            if (currentFeverBoss != null)
            {
                activeNotes.Add(currentFeverBoss);
                currentFeverBoss.Initialize(null, 0, RhythmConfig.Instance.LaneCount, bossHitTime, secondsPerBeat * BossWaitBeats, Mathf.RoundToInt(BossWaitBeats), NoteType.Fever);
            }
        }

        UpdateFeverUI();
        OnFeverStateChanged?.Invoke(active);
    }
}