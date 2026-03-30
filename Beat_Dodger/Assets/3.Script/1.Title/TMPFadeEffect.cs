using UnityEngine;
using TMPro;
using DG.Tweening;

namespace BeatDodger.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPFadeEffect : MonoBehaviour
    {
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private float _minAlpha = 0f;

        private TMP_Text _tmpText;

        private void Awake()
        {
            TryGetComponent(out _tmpText);
        }

        private void Start()
        {
            // 옵션을 제거하고 무조건 시작 시 깜빡이도록 단순화했습니다.
            StartBlinking();
        }

        public void SetAlpha(float alpha)
        {
            if (_tmpText != null) _tmpText.alpha = alpha;
        }

        public void FadeIn()
        {
            if (_tmpText == null) return;
            _tmpText.DOKill();
            // 정석적인 OutQuad 곡선을 내부적으로 고정했습니다.
            _tmpText.DOFade(1f, _duration).SetEase(Ease.OutQuad);
        }

        public void FadeOut()
        {
            if (_tmpText == null) return;
            _tmpText.DOKill();
            _tmpText.DOFade(0f, _duration).SetEase(Ease.OutQuad);
        }

        public void StartBlinking()
        {
            if (_tmpText == null) return;
            _tmpText.DOKill();
            _tmpText.DOFade(_minAlpha, _duration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        public void Stop()
        {
            _tmpText?.DOKill();
        }

        private void OnDestroy()
        {
            _tmpText?.DOKill();
        }
    }
}
