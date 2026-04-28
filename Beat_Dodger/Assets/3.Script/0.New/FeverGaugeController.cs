using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FeverGaugeController : MonoBehaviour
{
    [Header("Fever Gauge UI")]
    [SerializeField] private Image feverGaugeImage;

    [Header("Animation Settings")]
    [SerializeField] private float fillAnimationDuration = 0.2f;
    [Tooltip("게이지가 0일 때의 Fill Amount (기본 0.3)")]
    [SerializeField] private float minFillAmount = 0.3f;
    [Tooltip("게이지가 100%일 때의 Fill Amount (기본 1.0)")]
    [SerializeField] private float maxFillAmount = 1.0f;

    /// <summary>
    /// 현재 피버 수치와 최대치를 바탕으로 게이지 UI를 부드럽게 갱신합니다.
    /// </summary>
    public void UpdateFeverGauge(float currentValue, float maxValue)
    {
        if (feverGaugeImage == null || maxValue <= 0f) return;

        // 0.0 ~ 1.0 비율로 정규화
        float normalizedValue = Mathf.Clamp01(currentValue / maxValue);
        
        // 정규화된 값을 minFillAmount ~ maxFillAmount 사이로 보간
        float targetFillAmount = Mathf.Lerp(minFillAmount, maxFillAmount, normalizedValue);

        // 진행 중인 애니메이션 취소 후 새로운 목표치로 트윈 실행
        feverGaugeImage.DOKill();
        feverGaugeImage.DOFillAmount(targetFillAmount, fillAnimationDuration).SetEase(Ease.OutQuad);
    }

    /// <summary>
    /// 피버 지속 시간 동안 게이지를 100%에서 0%로 선형적으로 줄입니다.
    /// </summary>
    public void StartCountdown(float duration)
    {
        if (feverGaugeImage == null) return;

        feverGaugeImage.DOKill();
        feverGaugeImage.fillAmount = maxFillAmount;
        feverGaugeImage.DOFillAmount(minFillAmount, duration).SetEase(Ease.Linear);
    }

    /// <summary>
    /// 피버 모드 발동 시 등 게이지를 즉시 초기화하거나 특정 상태로 만들 때 사용합니다.
    /// </summary>
    public void ResetGauge()
    {
        if (feverGaugeImage == null) return;

        feverGaugeImage.DOKill();
        feverGaugeImage.fillAmount = minFillAmount;
    }

    private void OnDestroy()
    {
        // 파괴 시 메모리 누수 방지
        if (feverGaugeImage != null)
        {
            feverGaugeImage.DOKill();
        }
    }
}
