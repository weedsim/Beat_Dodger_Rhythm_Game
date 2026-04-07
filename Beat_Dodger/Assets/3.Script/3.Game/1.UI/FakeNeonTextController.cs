using UnityEngine;
using TMPro;

namespace BeatDodger.UI
{
    [ExecuteAlways]
    public class FakeNeonTextController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private TextMeshProUGUI _coreText;
        [SerializeField] private TextMeshProUGUI _glowText;

        [Header("Material Presets (Manual)")]
        // 사용자가 인스펙터 브라우저 상의 파일 형태(프리셋)를 직접 할당합니다.
        [SerializeField] private Material _corePreset;
        [SerializeField] private Material _glowPreset;

        [Header("Settings")]
        [SerializeField, TextArea] private string _content;

        private void Update()
        {
            if (_coreText == null || _glowText == null) return;

            SyncUI();
        }

        private void SyncUI()
        {
            // 1. 텍스트 내용 동기화
            if (_coreText.text != _content)
            {
                _coreText.text = _content;
                _glowText.text = _content;
            }

            // 2. 머티리얼 프리셋 강제 유지 (기본값 설정)
            // fontSharedMaterial을 사용해야 에디터 모드에서 프리셋 디자인을 쉽게 볼 수 있습니다.
            if (_corePreset != null && _coreText.fontSharedMaterial != _corePreset)
            {
                _coreText.fontSharedMaterial = _corePreset;
            }

            if (_glowPreset != null && _glowText.fontSharedMaterial != _glowPreset)
            {
                _glowText.fontSharedMaterial = _glowPreset;
            }

            // 3. 레이아웃 동기화
            if (_glowText.fontSize != _coreText.fontSize) _glowText.fontSize = _coreText.fontSize;
            if (_glowText.alignment != _coreText.alignment) _glowText.alignment = _coreText.alignment;
            _glowText.transform.localScale = Vector3.one;
        }

        public void SetText(string newText)
        {
            _content = newText;
            SyncUI();
        }

        public void SetColor(Color color)
        {
            // 1. Vertex Color 적용 (기본 색감)
            if (_coreText != null) _coreText.color = color;
            if (_glowText != null) _glowText.color = color;

            // 2. 머티리얼 속성 조절 (번짐 색상 변경)
            // .fontMaterial을 사용하여 프리셋 본체는 건드리지 않고 런타임 인스턴스(사본)에만 적용합니다.
            if (_coreText != null && _coreText.fontMaterial != null)
            {
                _coreText.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            }
            
            if (_glowText != null && _glowText.fontMaterial != null)
            {
                _glowText.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, color);
            }
        }

        public void SetFont(TMP_FontAsset newFont)
        {
            // 수동 프리셋 방식을 사용할 때는 프리셋 고유의 비주얼을 유지하기 위해 
            // 외부의 폰트 변경 요청을 명시적으로 제어할 수도 있습니다.
            // 필요 시 폰트를 강제 변경하지만, SyncUI에서 프리셋이 다시 입혀지게 됩니다.
            if (_coreText != null) _coreText.font = newFont;
            if (_glowText != null) _glowText.font = newFont;
        }
    }
}
