using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace BeatDodger.UI
{
    
    
    
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Color Settings")]
        [SerializeField] private Color _hoverColor = Color.cyan;
        [SerializeField] private float _transitionSpeed = 0.2f;

        [Header("Scale Settings")]
        [SerializeField] private float _hoverScale = 1.15f;

        private TMP_Text _text;
        private Color _originalColor;
        private Vector3 _originalScale;

        private void Awake()
        {
            if (TryGetComponent(out _text))
            {
                _originalColor = _text.color;
                _originalScale = transform.localScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_text == null) return;
            
            _text.DOKill();
            transform.DOKill();

            _text.DOColor(_hoverColor, _transitionSpeed).SetEase(Ease.OutQuad);
            transform.DOScale(_originalScale * _hoverScale, _transitionSpeed).SetEase(Ease.OutBack);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_text == null) return;

            _text.DOKill();
            transform.DOKill();

            _text.DOColor(_originalColor, _transitionSpeed).SetEase(Ease.OutQuad);
            transform.DOScale(_originalScale, _transitionSpeed).SetEase(Ease.OutQuad);
        }

        private void OnDestroy()
        {
            _text?.DOKill();
            transform.DOKill();
        }
    }
}
