using UnityEngine;
using DG.Tweening;

public class LaneInputEffect : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float jumpAmount = 0.5f;
    [SerializeField] private float scaleAmount = 0.2f;
    [SerializeField] private float duration = 0.1f;
    [SerializeField] private float inputCooldown = 0.05f; // 아주 짧은 간격으로 재발동 방지

    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Sequence currentSequence;
    private float lastPlayTime = -1f;

    private void Awake()
    {
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;
    }

    public void PlayEffect()
    {
        // 쿨타임 체크: 뗄 때(Release) 호출되어도 무시하도록 설정
        if (Time.time < lastPlayTime + inputCooldown)
        {
            return;
        }

        lastPlayTime = Time.time;

        // 기존 애니메이션 초기화
        if (currentSequence != null)
        {
            currentSequence.Kill(true);
        }

        transform.localPosition = originalPosition;
        transform.localScale = originalScale;

        currentSequence = DOTween.Sequence();

        // 1. 위로 올라가면서 커짐
        currentSequence.Join(transform.DOLocalMoveY(originalPosition.y + jumpAmount, duration).SetEase(Ease.OutQuad));
        currentSequence.Join(transform.DOScale(originalScale + (originalScale * scaleAmount), duration).SetEase(Ease.OutQuad));

        // 2. 다시 원래대로 복구
        currentSequence.Append(transform.DOLocalMoveY(originalPosition.y, duration).SetEase(Ease.InQuad));
        currentSequence.Join(transform.DOScale(originalScale, duration).SetEase(Ease.InQuad));

        currentSequence.OnKill(() => {
            transform.localPosition = originalPosition;
            transform.localScale = originalScale;
        });
    }

    private void OnDestroy()
    {
        currentSequence?.Kill();
    }
}
