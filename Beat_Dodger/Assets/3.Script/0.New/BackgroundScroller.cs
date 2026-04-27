using UnityEngine;
using DG.Tweening;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Scrolling Settings")]
    [SerializeField] private Vector2 scrollDirection = new Vector2(0, 1);
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private string texturePropertyName = "_BaseMap";
    [SerializeField] private Ease scrollEase = Ease.InOutSine;
    
    private MeshRenderer meshRenderer;
    private Material targetMaterial;
    private Vector2 currentOffset;
    private Tween currentTween;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            targetMaterial = meshRenderer.material;
        }
    }

    private void OnEnable()
    {
        NewRhythmManager.OnBeat += HandleBeat;
    }

    private void OnDisable()
    {
        NewRhythmManager.OnBeat -= HandleBeat;
    }

    private void HandleBeat()
    {
        if (targetMaterial == null || NewRhythmManager.Instance == null) return;

        float beatDuration = 60f / NewRhythmManager.Instance.BPM;
        float delay = beatDuration * 0.7f;
        float moveDuration = beatDuration * 0.3f;
        
        currentOffset += scrollDirection * scrollSpeed;
        
        // 정박의 70% 지점까지 대기 후 30% 시간 동안 부드럽게 이동
        currentTween?.Kill();
        currentTween = targetMaterial.DOOffset(currentOffset, texturePropertyName, moveDuration)
            .SetDelay(delay)
            .SetEase(scrollEase);
    }

    private void Update()
    {
        // Continuous update removed to ensure movement only on beat
    }

    private void OnDestroy()
    {
        if (targetMaterial != null)
        {
            Destroy(targetMaterial);
        }
    }
}
