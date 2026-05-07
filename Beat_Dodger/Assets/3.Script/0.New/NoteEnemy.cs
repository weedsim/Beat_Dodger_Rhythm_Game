using UnityEngine;
using UnityEngine.Pool;

public enum NoteType { Normal, Dash, Double, Fever } // Dash: 돌진형, Double: 2연타, Fever: 피버용

public class NoteEnemy : MonoBehaviour
{
    private const float StepStartThreshold = 0.5f;

    [SerializeField] private int startLane; // 시작 레인
    private int laneSpan = 1;              // 차지하는 레인 개수
    public int StartLane => startLane;
    public int LaneSpan => laneSpan;

    private IObjectPool<NoteEnemy> pool;
    private double targetHitTime;
    public double TargetHitTime => targetHitTime;

    private float noteDurationSeconds;
    private int beatsToArrive;
    private NoteType type;
    public NoteType Type => type;
    private int hitsRemaining;
    public int HitsRemaining => hitsRemaining;
    public bool HasContributedToFever { get; set; }
    
    private int hitLanesMask;

    private Transform cachedTransform;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

    private Vector3 originalScale; // 프리팹 원래 스케일 저장
    private float cachedXPosition;
    private Vector3 cachedFinalScale;
    private GameObject connectionEffectInstance;

    private void Awake()
    {
        cachedTransform = transform;
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        propBlock = new MaterialPropertyBlock();
        originalScale = transform.localScale;
    }

    public void Initialize(IObjectPool<NoteEnemy> enemyPool, int startLane, int span, double hitTime, float duration, int beats, NoteType noteType = NoteType.Normal)
    {
        pool = enemyPool;
        this.startLane = startLane;
        this.laneSpan = span;
        targetHitTime = hitTime;
        noteDurationSeconds = duration;
        beatsToArrive = beats;
        type = noteType;
        
        // For chords, we need all players to hit. If Double, they must all hit twice.
        hitsRemaining = laneSpan * (type == NoteType.Double ? 2 : 1);
        hitLanesMask = 0; // Bitmask reset
        HasContributedToFever = false;

        if (connectionEffectInstance != null)
        {
            connectionEffectInstance.SetActive(false); // 즉시 비활성화하여 1프레임 동안 파티클이 새는 현상 방지
            Destroy(connectionEffectInstance);
            connectionEffectInstance = null;
        }

        // Cache positions and scales that don't change
        float centerLane = startLane + (laneSpan - 1) / 2f;
        cachedXPosition = (centerLane - (RhythmConfig.Instance.LaneCount / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
        
        cachedFinalScale = originalScale;
        if (laneSpan > 1)
        {
            cachedFinalScale.x = originalScale.x + (laneSpan - 1) * RhythmConfig.Instance.LaneSpacing;
        }

        ApplyTypeColor();
        UpdatePosition(0f);
    }

    public void MarkLaneHit(int lane)
    {
        if (type == NoteType.Double) return;

        int localIndex = lane - startLane;
        if (localIndex >= 0 && localIndex < 32) // Use bitmask (max 32 lanes, but usually 4)
        {
            hitLanesMask |= (1 << localIndex);
        }
    }

    public bool IsLaneAlreadyHit(int lane)
    {
        if (type == NoteType.Double) return false;

        int localIndex = lane - startLane;
        if (localIndex >= 0 && localIndex < 32)
        {
            return (hitLanesMask & (1 << localIndex)) != 0;
        }
        return false;
    }

    public bool IsOccupyingLane(int lane)
    {
        return lane >= startLane && lane < startLane + laneSpan;
    }

    private void Update()
    {
        float progress = CalculateProgress();
        UpdatePosition(progress);

        if (progress > 1.0f + (RhythmConfig.Instance.MissThreshold / noteDurationSeconds))
        {
            NewRhythmManager.Instance?.ReportMiss(this);
            ReleaseToPool();
        }
    }

    private float CalculateProgress()
    {
        double timeUntilHit = targetHitTime - AudioSettings.dspTime;
        return 1f - (float)(timeUntilHit / noteDurationSeconds);
    }

    private void UpdatePosition(float progress)
    {
        float finalProgress = 0f;

        if (type == NoteType.Dash)
        {
            float stopProgressThreshold = (float)(beatsToArrive - 2.0f) / beatsToArrive;
            if (progress < stopProgressThreshold) finalProgress = CalculateSteppedProgress(progress, false);
            else if (progress < 0.96f) finalProgress = CalculateSteppedProgress(stopProgressThreshold, false);
            else
            {
                float dashProgress = Mathf.InverseLerp(0.96f, 1.0f, progress);
                finalProgress = Mathf.Lerp(CalculateSteppedProgress(stopProgressThreshold, false), 1f, dashProgress);
            }
        }
        else
        {
            finalProgress = CalculateSteppedProgress(progress, true);
        }
        
        cachedTransform.localScale = cachedFinalScale;

        Vector3 startPosition = new Vector3(cachedXPosition, RhythmConfig.Instance.YOffset, RhythmConfig.Instance.SpawnLineZ);
        Vector3 endPosition = new Vector3(cachedXPosition, RhythmConfig.Instance.YOffset, RhythmConfig.Instance.JudgeLineZ);

        cachedTransform.localPosition = Vector3.Lerp(startPosition, endPosition, finalProgress);
    }

    private float CalculateSteppedProgress(float progress, bool useGlobalSync)
    {
        if (NewRhythmManager.Instance == null) return progress;

        float bpm = NewRhythmManager.Instance.BPM;
        float secondsPerBeat = 60f / bpm;
        
        float secondsPerStep = secondsPerBeat;
        
        double songStartTime = NewRhythmManager.Instance.SongStartTime;
        
        // 1. 시간 기준점 결정
        double referenceTime = useGlobalSync ? AudioSettings.dspTime : targetHitTime - ((1f - progress) * noteDurationSeconds);
        
        // 2. 글로벌 스텝 계산
        double timeSinceStart = referenceTime - songStartTime;
        float globalStep = (float)(timeSinceStart / secondsPerStep);
        int floorGlobalStep = Mathf.FloorToInt(globalStep + 0.001f);
        float innerProgress = globalStep - floorGlobalStep;
        
        // 3. 목표 스텝 및 남은 단계 계산
        float targetStep = (float)((targetHitTime - songStartTime) / secondsPerStep);
        int stepsRemaining = Mathf.FloorToInt(targetStep - floorGlobalStep + 0.001f);
        
        // 4. 스텝 위치 계산
        int totalSteps = Mathf.RoundToInt(beatsToArrive);
        float stepBase = 1f - (float)stepsRemaining / totalSteps;
        float nextStepBase = 1f - (float)(stepsRemaining - 1) / totalSteps;

        // 5. 정박 70% 지점에서 다음 칸으로 스윽 이동 (Stay for 70%, Slide for 30%)
        float slideStartThreshold = 0.7f;
        float movementCurve = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(slideStartThreshold, 1.0f, innerProgress));
        return Mathf.Lerp(stepBase, nextStepBase, movementCurve);
    }
    private void ApplyTypeColor()
    {
        if (meshRenderer == null) return;
        Color targetColor = type switch
        {
            NoteType.Dash => Color.red,
            NoteType.Double => Color.yellow,
            NoteType.Fever => new Color(1f, 0.8f, 0.2f), // Gold/Amber for Fever notes
            _ => new Color(0.2f, 0.6f, 1f)
        };
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(ColorProperty, targetColor);
        meshRenderer.SetPropertyBlock(propBlock);
    }

    public void OnHit()
    {
        hitsRemaining--;

        // For Double notes, reset the hit mask after the first full set of hits 
        // so players can hit the same lanes again for the second set.
        if (type == NoteType.Double && hitsRemaining == laneSpan)
        {
            hitLanesMask = 0;
        }

        if (hitsRemaining <= 0) ReleaseToPool();
    }

    public void AddConnectionEffect(GameObject prefab, Vector3 worldOffset, int span)
    {
        if (prefab == null) return;
        
        // SkinnedMeshRenderer의 메쉬 오브젝트 자체는 움직이지 않고 뼈대(Bone)가 움직이므로,
        // rootBone을 찾아 부모로 설정해야 애니메이션(점프 등)을 정상적으로 따라갑니다.
        Transform attachParent = transform;
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        
        if (smr != null && smr.rootBone != null) attachParent = smr.rootBone;
        else if (smr != null) attachParent = smr.transform;
        else if (transform.childCount > 0) attachParent = transform.GetChild(0);

        // 부모의 복잡한 스케일/회전에 영향받지 않도록 먼저 최상단(월드)에 생성
        connectionEffectInstance = Instantiate(prefab);
        
        // 월드 기준 위치 및 회전 설정
        connectionEffectInstance.transform.position = transform.position + worldOffset;
        connectionEffectInstance.transform.rotation = Quaternion.Euler(0, 90, 0);

        // 월드 기준 스케일 설정
        Vector3 globalScale = connectionEffectInstance.transform.localScale;
        if (span == 2) globalScale.z = 0.25f;
        else if (span == 3) globalScale.z = 0.6f;
        else if (span >= 4) globalScale.z = 0.75f;
        connectionEffectInstance.transform.localScale = globalScale;
        
        // 설정이 끝난 후 부모에 종속 (worldPositionStays를 true로 하여 월드 좌표/회전/크기를 그대로 유지)
        connectionEffectInstance.transform.SetParent(attachParent, true);
    }

    public void ReleaseToPool()
    {
        if (connectionEffectInstance != null)
        {
            connectionEffectInstance.SetActive(false);
            Destroy(connectionEffectInstance);
            connectionEffectInstance = null;
        }

        if (pool != null) pool.Release(this);
        else Destroy(gameObject); // For non-pooled objects like the Boss
    }
}