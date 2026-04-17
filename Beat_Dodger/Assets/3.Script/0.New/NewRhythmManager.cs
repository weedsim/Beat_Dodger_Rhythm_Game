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
    [SerializeField] private int beatsToArrive = 4;
    public int BeatsToArrive => beatsToArrive;

    [Header("References")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private LaneInputEffect[] inputEffects;

    private IObjectPool<NoteEnemy> enemyPool;
    private readonly List<NoteEnemy> activeNotes = new List<NoteEnemy>();

    // 상태 관리
    private int currentCombo;
    private int maxCombo;
    private float secondsPerBeat;
    private float noteDuration;
    private double nextBeatTime;

    // 이벤트 (UI 및 시스템 연동용)
    public static event Action OnBeat;
    public event Action<Judgment, int> OnNoteHit; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        InitializeRhythmSettings();
        SetupObjectPool();
    }

    private void InitializeRhythmSettings()
    {
        secondsPerBeat = 60f / bpm;
        noteDuration = secondsPerBeat * beatsToArrive;
    }

    private void SetupObjectPool()
    {
        enemyPool = new ObjectPool<NoteEnemy>(
            createFunc: CreateNoteEnemy,
            actionOnGet: OnGetNoteFromPool,
            actionOnRelease: OnReleaseNoteToPool,
            actionOnDestroy: OnDestroyNoteFromPool,
            collectionCheck: false,
            defaultCapacity: 10,
            maxSize: 30
        );
    }

    private NoteEnemy CreateNoteEnemy()
    {
        GameObject noteInstance = Instantiate(enemyPrefab);
        return noteInstance.TryGetComponent<NoteEnemy>(out NoteEnemy noteEnemy) ? noteEnemy : noteInstance.AddComponent<NoteEnemy>();
    }

    private void OnGetNoteFromPool(NoteEnemy noteEnemy)
    {
        noteEnemy.gameObject.SetActive(true);
        activeNotes.Add(noteEnemy);
    }

    private void OnReleaseNoteToPool(NoteEnemy noteEnemy)
    {
        noteEnemy.gameObject.SetActive(false);
        activeNotes.Remove(noteEnemy);
    }

    private void OnDestroyNoteFromPool(NoteEnemy noteEnemy) { Destroy(noteEnemy.gameObject); }

    private void Start()
    {
        nextBeatTime = AudioSettings.dspTime + secondsPerBeat;
    }

    private void Update()
    {
        double currentTime = AudioSettings.dspTime;

        if (currentTime >= nextBeatTime)
        {
            GeneratePattern();
            nextBeatTime += secondsPerBeat;
            OnBeat?.Invoke();
        }
    }

    private void GeneratePattern()
    {
        float rand = UnityEngine.Random.value;

        if (rand < 0.15f) SpawnChordPattern();       // 동시치기 (15%)
        else if (rand < 0.25f) SpawnDoubleTapPattern(); // 2연타 (10%)
        else if (rand < 0.35f) SpawnDashPattern();    // 돌진 (10%)
        else if (rand < 0.45f) SpawnOffBeatPattern(); // 엇박 (10%)
        else SpawnIndividualNote(UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount), 1, nextBeatTime + noteDuration, NoteType.Normal); // 일반 (55%)
    }

    // 단일 및 거대 노트 생성 공통 로직
    private void SpawnIndividualNote(int startLane, int span, double hitTime, NoteType type)
    {
        NoteEnemy enemy = enemyPool.Get();
        enemy.Initialize(enemyPool, startLane, span, hitTime, noteDuration, beatsToArrive, type);
    }

    // 1. 인접 동시치기 (길게 연결된 하나의 거대 노트 생성)
    private void SpawnChordPattern()
    {
        int totalLanes = RhythmConfig.Instance.LaneCount;
        int span = UnityEngine.Random.Range(2, totalLanes + 1); // 2~4개 레인 차지
        int startLane = UnityEngine.Random.Range(0, totalLanes - span + 1);

        SpawnIndividualNote(startLane, span, nextBeatTime + noteDuration, NoteType.Normal);
    }

    // 2. 2연타 (하나의 노트에 내구도 2 설정)
    private void SpawnDoubleTapPattern()
    {
        int lane = UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount);
        SpawnIndividualNote(lane, 1, nextBeatTime + noteDuration, NoteType.Double);
    }

    // 3. 돌진형 (멈췄다 돌진)
    private void SpawnDashPattern()
    {
        int lane = UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount);
        SpawnIndividualNote(lane, 1, nextBeatTime + noteDuration, NoteType.Dash);
    }

    // 4. 엇박 (0.5박자 뒤에 생성)
    private void SpawnOffBeatPattern()
    {
        int lane = UnityEngine.Random.Range(0, RhythmConfig.Instance.LaneCount);
        SpawnIndividualNote(lane, 1, nextBeatTime + noteDuration + (secondsPerBeat * 0.5f), NoteType.Normal);
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
        NoteEnemy closestNote = null;
        double minTimeOffset = double.MaxValue;

        // 가장 가까운(정확한 타격 시점) 노트를 찾음
        foreach (var note in activeNotes)
        {
            // 이제 단순히 Index 비교가 아닌 IsOccupyingLane을 사용함
            if (!note.IsOccupyingLane(laneIndex)) continue;

            double timeOffset = Math.Abs(AudioSettings.dspTime - note.TargetHitTime);
            if (timeOffset <= RhythmConfig.Instance.GoodThreshold && timeOffset < minTimeOffset)
            {
                minTimeOffset = timeOffset;
                closestNote = note;
            }
        }

        if (closestNote != null)
        {
            Judgment result = EvaluateJudgment(minTimeOffset);
            ApplyHitResult(result);
            closestNote.OnHit();
        }
    }

    private Judgment EvaluateJudgment(double timeOffset)
    {
        if (timeOffset <= RhythmConfig.Instance.PerfectThreshold) return Judgment.Perfect;
        if (timeOffset <= RhythmConfig.Instance.GreatThreshold) return Judgment.Great;
        if (timeOffset <= RhythmConfig.Instance.GoodThreshold) return Judgment.Good;
        return Judgment.Miss;
    }

    private void ApplyHitResult(Judgment result)
    {
        if (result == Judgment.Miss)
        {
            ResetCombo();
        }
        else
        {
            currentCombo++;
            maxCombo = Math.Max(maxCombo, currentCombo);
        }

        OnNoteHit?.Invoke(result, currentCombo);
        
        // 디버그용 출력 (나중에 UI 연결 시 제거 가능)
        Debug.Log($"Hit! [{result}] Combo: {currentCombo}");
    }

    public void ReportMiss(NoteEnemy note)
    {
        ResetCombo();
        OnNoteHit?.Invoke(Judgment.Miss, currentCombo);
        Debug.Log("Missed!");
    }

    private void ResetCombo()
    {
        currentCombo = 0;
    }
}