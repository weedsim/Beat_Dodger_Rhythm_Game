using UnityEngine;
using DG.Tweening;

public class RhythmFloorMover : MonoBehaviour
{
    [SerializeField] private Ease moveEase = Ease.OutSine;

    private Vector3 initialPosition;
    private float stepDist;
    private Tween currentTween;

    private void Awake()
    {
        initialPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        NewRhythmManager.OnBeat += HandleBeat;
    }

    private void OnDisable()
    {
        NewRhythmManager.OnBeat -= HandleBeat;
    }

    private void Start()
    {
        CalculateStepDistance();
    }

    private void CalculateStepDistance()
    {
        if (RhythmConfig.Instance == null || NewRhythmManager.Instance == null) return;

        float spawnZ = RhythmConfig.Instance.SpawnLineZ;
        float judgeZ = RhythmConfig.Instance.JudgeLineZ;
        int beatsToArrive = NewRhythmManager.Instance.BeatsToArrive;

        stepDist = (spawnZ - judgeZ) / beatsToArrive;
    }

    private void HandleBeat()
    {
        if (NewRhythmManager.Instance == null) return;

        // 1박자 무한 루프: 매 박자마다 위치를 초기화
        // RhythmColorSwitcher가 매 박자마다 색상을 교차(Flip)시키므로,
        // 1박자 전진 후 다시 원위치로 오면 색상과 위치가 완벽히 일치하게 됩니다.
        transform.localPosition = initialPosition;

        float beatDuration = 60f / NewRhythmManager.Instance.BPM;
        float delay = beatDuration * 0.7f;
        float moveDuration = beatDuration * 0.3f;
        
        // 목표 지점은 항상 1박자 전진한 곳 (-stepDist)
        float targetZ = initialPosition.z - stepDist;
        
        currentTween?.Kill();
        currentTween = transform.DOLocalMoveZ(targetZ, moveDuration)
            .SetDelay(delay)
            .SetEase(moveEase);
    }

    public void ResetFloor()
    {
        currentTween?.Kill();
        transform.localPosition = initialPosition;
    }
}
