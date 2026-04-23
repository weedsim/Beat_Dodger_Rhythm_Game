using UnityEngine;
using UnityEngine.Pool;

public enum NoteType { Normal, Dash, Double, OffBeat, Fever } // Dash: 돌진형, Double: 2연타, OffBeat: 엇박, Fever: 피버용

public class NoteEnemy : MonoBehaviour
{
    public int myNoteId = -1;

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
    
    private bool[] hitLanesMask;

    private Transform cachedTransform;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_BaseColor");

    private Vector3 originalScale; // 프리팹 원래 스케일 저장

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
        
        // 동시치기 거대 노트는 차지하는 칸 수만큼 타격 필요
        hitsRemaining = (type == NoteType.Double) ? 2 : laneSpan;
        hitLanesMask = new bool[laneSpan];

        ApplyTypeColor();
        UpdatePosition(0f);
    }

    public void MarkLaneHit(int lane)
    {
        if (type == NoteType.Double) return;

        int localIndex = lane - startLane;
        if (localIndex >= 0 && localIndex < hitLanesMask.Length)
        {
            hitLanesMask[localIndex] = true;
        }
    }

    public bool IsLaneAlreadyHit(int lane)
    {
        if (type == NoteType.Double) return false;

        int localIndex = lane - startLane;
        if (localIndex >= 0 && localIndex < hitLanesMask.Length)
        {
            return hitLanesMask[localIndex];
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
            if (progress < stopProgressThreshold) finalProgress = CalculateSteppedProgress(progress);
            else if (progress < 0.96f) finalProgress = CalculateSteppedProgress(stopProgressThreshold);
            else
            {
                float dashProgress = Mathf.InverseLerp(0.96f, 1.0f, progress);
                finalProgress = Mathf.Lerp(CalculateSteppedProgress(stopProgressThreshold), 1f, dashProgress);
            }
        }
        else
        {
            finalProgress = CalculateSteppedProgress(progress);
        }
        
        float centerLane = startLane + (laneSpan - 1) / 2f;
        float xPosition = (centerLane - (RhythmConfig.Instance.LaneCount / 2f - 0.5f)) * RhythmConfig.Instance.LaneSpacing;
        
        Vector3 finalScale = originalScale;
        if (laneSpan > 1)
        {
            finalScale.x = originalScale.x + (laneSpan - 1) * RhythmConfig.Instance.LaneSpacing;
        }
        cachedTransform.localScale = finalScale;

        Vector3 startPosition = new Vector3(xPosition, RhythmConfig.Instance.YOffset, RhythmConfig.Instance.SpawnLineZ);
        Vector3 endPosition = new Vector3(xPosition, RhythmConfig.Instance.YOffset, RhythmConfig.Instance.JudgeLineZ);

        cachedTransform.localPosition = Vector3.Lerp(startPosition, endPosition, finalProgress);
    }

    private float CalculateSteppedProgress(float progress)
    {
        int totalSteps = beatsToArrive;
        int currentStep = Mathf.FloorToInt(progress * totalSteps);
        float stepBase = (float)currentStep / totalSteps;
        float nextStepBase = (float)(currentStep + 1) / totalSteps;
        float innerProgress = (progress * totalSteps) - currentStep;
        float movementCurve = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(StepStartThreshold, 1.0f, innerProgress));
        return Mathf.Lerp(stepBase, nextStepBase, movementCurve);
    }

    private void ApplyTypeColor()
    {
        if (meshRenderer == null) return;
        Color targetColor = type switch
        {
            NoteType.Dash => Color.red,
            NoteType.Double => Color.yellow,
            NoteType.OffBeat => Color.green,
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
        if (hitsRemaining <= 0) ReleaseToPool();
    }

    public void ReleaseToPool()
    {
        if (pool != null) pool.Release(this);
    }
}