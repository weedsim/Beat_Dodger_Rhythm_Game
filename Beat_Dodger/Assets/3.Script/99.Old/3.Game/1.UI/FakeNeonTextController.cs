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
            
            if (_coreText.text != _content)
            {
                _coreText.text = _content;
                _glowText.text = _content;
            }

            
            
            if (_corePreset != null && _coreText.fontSharedMaterial != _corePreset)
            {
                _coreText.fontSharedMaterial = _corePreset;
            }

            if (_glowPreset != null && _glowText.fontSharedMaterial != _glowPreset)
            {
                _glowText.fontSharedMaterial = _glowPreset;
            }

            
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
            
            if (_coreText != null) _coreText.color = color;
            if (_glowText != null) _glowText.color = color;

            
            
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
            
            
            
            if (_coreText != null) _coreText.font = newFont;
            if (_glowText != null) _glowText.font = newFont;
        }
    }
}
