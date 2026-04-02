using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace BeatDodger.Game
{
    /// <summary>
    /// Professional Health UI with smooth slider transitions and warning colors.
    /// Incorporates loss feedback (shake) for improved player awareness.
    /// </summary>
    public class HealthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform shakeTarget; // 흔들림을 적용할 대상 (비어있으면 슬라이더 전체)

        [Header("Animation Settings")]
        [SerializeField] private float smoothTime = 0.25f;
        [SerializeField] private float shakeIntensity = 10f; // 기본 강도를 조금 더 높임
        [SerializeField] private float shakeDuration = 0.15f;

        [Header("Visual Colors")]
        [SerializeField] private Color normalColor = Color.green;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;

        private float targetValue;
        private Coroutine sliderCoroutine;
        private Coroutine shakeCoroutine;
        private Vector2 initialAnchoredPosition;

        private void Awake()
        {
            InitializeShakeTarget();
        }

        private void InitializeShakeTarget()
        {
            // shakeTarget이 지정되어 있지 않으면 슬라이더 자체를 대상으로 함
            if (shakeTarget == null && healthSlider != null)
            {
                shakeTarget = healthSlider.GetComponent<RectTransform>();
            }

            if (shakeTarget != null)
            {
                initialAnchoredPosition = shakeTarget.anchoredPosition;
            }
        }

        public void Init(int maxHealth)
        {
            if (shakeTarget == null) InitializeShakeTarget();

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = maxHealth;
                targetValue = maxHealth;
            }
            UpdateColor(maxHealth / (float)maxHealth);
        }

        public void UpdateUI(int currentHealth)
        {
            targetValue = currentHealth;

            // 1. Smooth Slider Transition
            if (sliderCoroutine != null) StopCoroutine(sliderCoroutine);
            if (gameObject.activeInHierarchy) sliderCoroutine = StartCoroutine(SmoothSliderUpdate());

            // 2. Shake Feedback (Juice)
            if (gameObject.activeInHierarchy && shakeTarget != null)
            {
                if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
                shakeCoroutine = StartCoroutine(ShakeAnimation());
            }
        }

        private IEnumerator SmoothSliderUpdate()
        {
            float startValue = healthSlider.value;
            float elapsed = 0f;

            while (elapsed < smoothTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / smoothTime;
                t = 1f - Mathf.Pow(1f - t, 3f);
                
                healthSlider.value = Mathf.Lerp(startValue, targetValue, t);
                UpdateColor(healthSlider.value / healthSlider.maxValue);
                yield return null;
            }

            healthSlider.value = targetValue;
            UpdateColor(healthSlider.value / healthSlider.maxValue);
        }

        private void UpdateColor(float percent)
        {
            if (fillImage != null)
            {
                if (percent > 0.5f) fillImage.color = Color.Lerp(warningColor, normalColor, (percent - 0.5f) * 2f);
                else fillImage.color = Color.Lerp(criticalColor, warningColor, percent * 2f);
            }
        }

        private IEnumerator ShakeAnimation()
        {
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                Vector2 randomOffset = Random.insideUnitCircle * shakeIntensity;
                shakeTarget.anchoredPosition = initialAnchoredPosition + randomOffset;
                yield return null;
            }
            shakeTarget.anchoredPosition = initialAnchoredPosition;
        }
    }
}
