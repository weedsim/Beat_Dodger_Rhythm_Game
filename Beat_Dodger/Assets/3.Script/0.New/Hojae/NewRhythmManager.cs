using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.InputSystem;
using Mirror;

public class NewRhythmManager : NetworkBehaviour
{
    public static NewRhythmManager Instance { get; private set; }


    [Header("Sync Settings")]
    [SyncVar] public double exactStartTime; // 다 같이 시작할 절대 시간!
    private HashSet<int> hitNoteIds = new HashSet<int>(); // 이미 맞춘 노트 명부
    private int _globalNoteId = 0; // 번호표 기계

    [Header("Rhythm Settings")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private int beatsToArrive = 4;
    public int BeatsToArrive => beatsToArrive;

    [Header("References")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private LaneInputEffect[] inputEffects;
    [SerializeField] private GameObject hitEffectPerfect; // Perfect & Great용
    [SerializeField] private GameObject hitEffectGood;    // Good용

    private IObjectPool<NoteEnemy> enemyPool;
    private IObjectPool<GameObject> poolPerfect;
    private IObjectPool<GameObject> poolGood;
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

    private bool isGameStart = false;
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

        // Perfect & Great 이펙트 풀
        poolPerfect = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(hitEffectPerfect),
            actionOnGet: (go) => go.SetActive(true),
            actionOnRelease: (go) => go.SetActive(false),
            actionOnDestroy: (go) => Destroy(go),
            collectionCheck: false, defaultCapacity: 5, maxSize: 10
        );

        // Good 이펙트 풀
        poolGood = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(hitEffectGood),
            actionOnGet: (go) => go.SetActive(true),
            actionOnRelease: (go) => go.SetActive(false),
            actionOnDestroy: (go) => Destroy(go),
            collectionCheck: false, defaultCapacity: 5, maxSize: 10
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
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Debug.Log($"[띠또 보고] 엔터키 눌림! 방장 맞음?: {isServer} / 이미 시작함?: {isGameStart}");

            if (isServer && !isGameStart)
            {
                RpcStartMultiGame();
            }
        }

        if (!isGameStart) return;
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
        enemy.myNoteId = _globalNoteId;
        _globalNoteId++;
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
            // 해당 레인을 포함하는지 + 해당 레인을 아직 안 쳤는지 확인
            if (!note.IsOccupyingLane(laneIndex) || note.IsLaneAlreadyHit(laneIndex)) continue;

            double timeOffset = Math.Abs(AudioSettings.dspTime - note.TargetHitTime);
            
            // 연타(Double) 노트이면서 이미 한 번 타격된 경우 판정 범위를 2배로 확장 (보정)
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
            // 해당 레인 타격 성공 기록
            closestNote.MarkLaneHit(laneIndex);

            // 보정된 배율을 사용하여 판정 등급 결정
            float thresholdMultiplier = (closestNote.Type == NoteType.Double && closestNote.HitsRemaining < 2) ? 2.0f : 1.0f;
            Judgment result = EvaluateJudgment(minTimeOffset, thresholdMultiplier);
            
            ApplyHitResult(result, laneIndex);
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

    private void ApplyHitResult(Judgment result, int laneIndex)
    {
        if (result == Judgment.Miss)
        {
            ResetCombo();
        }
        else
        {
            currentCombo++;
            maxCombo = Math.Max(maxCombo, currentCombo);
            SpawnHitEffect(result, laneIndex); // 이펙트 생성
        }

        // 레인별 판정 텍스트 출력
        JudgmentUIController.Instance?.DisplayJudgment(laneIndex, result);

        OnNoteHit?.Invoke(result, currentCombo);
        Debug.Log($"Hit! [{result}] Combo: {currentCombo}");
    }

    private void SpawnHitEffect(Judgment result, int laneIndex)
    {
        IObjectPool<GameObject> targetPool = (result == Judgment.Perfect || result == Judgment.Great) ? poolPerfect : poolGood;
        GameObject effect = targetPool.Get();

        // 위치 설정: 해당 레인의 X 좌표, 판정선 Z 좌표
        float xPos = (laneIndex - (RhythmConfig.Instance.LaneCount / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
        effect.transform.position = new Vector3(xPos, 0.1f, RhythmConfig.Instance.JudgeLineZ);

        // 일정 시간 후 반환 (이펙트 재생 시간 고려, 기본 1초)
        StartCoroutine(ReturnToPoolAfterDelay(effect, targetPool, 1.0f));
    }

    private System.Collections.IEnumerator ReturnToPoolAfterDelay(GameObject effect, IObjectPool<GameObject> pool, float delay)
    {
        yield return new WaitForSeconds(delay);
        pool.Release(effect);
    }

    public void ReportMiss(NoteEnemy note)
    {
        ResetCombo();
        
        // 거대 노트의 경우, 차지하는 모든 레인 중 '안 친' 레인들에만 MISS 출력
        for (int i = note.StartLane; i < note.StartLane + note.LaneSpan; i++)
        {
            if (!note.IsLaneAlreadyHit(i))
            {
                JudgmentUIController.Instance?.DisplayJudgment(i, Judgment.Miss);
            }
        }

        OnNoteHit?.Invoke(Judgment.Miss, currentCombo);
        Debug.Log("Missed!");
    }

    private void ResetCombo()
    {
        currentCombo = 0;
    }
    [Command(requiresAuthority = false)]
    public void CmdRequestHitNote(int noteId, NetworkConnectionToClient sender = null)
    {
        if (hitNoteIds.Contains(noteId)) return; // 딴 놈이 쳤으면 무시!
        hitNoteIds.Add(noteId);
        RpcNotifyHit(noteId, sender.connectionId);
    }

    [ClientRpc]
    private void RpcNotifyHit(int noteId, int playerConnId)
    {
        // 1. 여기서 noteId를 가진 노트를 찾아서 화면에서 없앱니다!
        NoteEnemy targetNote = activeNotes.Find(n => n.myNoteId == noteId);
        if (targetNote != null)
        {
            activeNotes.Remove(targetNote);
            targetNote.gameObject.SetActive(false); // 또는 풀로 반환

            // 2. 이펙트 빵! (주인님 기존 이펙트 함수 호출)
            // SpawnHitEffect(Judgment.Perfect, targetNote.StartLane);
        }
    }
    [ClientRpc]
    private void RpcStartMultiGame()
    {
       isGameStart = true; // 자물쇠 해제!

        secondsPerBeat = 60f / bpm;
        noteDuration = beatsToArrive * secondsPerBeat;

        double startDelay = 3.0;
        nextBeatTime = AudioSettings.dspTime + startDelay;

        AudioSource audio = GetComponent<AudioSource>();
        if (audio != null && audio.clip != null)
        {
            audio.PlayScheduled(nextBeatTime);
        }
        else
        {
            Debug.LogWarning("주인님! 오디오 소스에 음악(Clip)이 안 들어있사옵니다!");
        }

        Debug.Log("멀티 리듬 게임 진짜 시작!! 노트야 쏟아져라!!");
    }
}