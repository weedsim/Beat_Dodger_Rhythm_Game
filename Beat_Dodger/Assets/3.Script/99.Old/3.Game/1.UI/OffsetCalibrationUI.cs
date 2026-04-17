using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace BeatDodger.Game
{
    
    
    
    public class OffsetCalibrationUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RhythmManager rhythmManager;
        [SerializeField] private TextMeshProUGUI offsetValueText;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private GameObject uiPanel;

        private float _currentOffset;

        private void Start()
        {
            if (rhythmManager == null) rhythmManager = Object.FindAnyObjectByType<RhythmManager>();
            
            
            _currentOffset = PlayerPrefs.GetFloat("RhythmOffset", 0f);
            ApplyOffset();

            if (increaseButton != null) increaseButton.onClick.AddListener(() => ChangeOffset(0.01f));
            if (decreaseButton != null) decreaseButton.onClick.AddListener(() => ChangeOffset(-0.01f));
            if (saveButton != null) saveButton.onClick.AddListener(SaveOffset);
            
            UpdateUI();
        }

        public void TogglePanel()
        {
            if (uiPanel != null) uiPanel.SetActive(!uiPanel.activeSelf);
        }

        private void ChangeOffset(float delta)
        {
            _currentOffset += delta;
            _currentOffset = Mathf.Clamp(_currentOffset, -1f, 1f); 
            ApplyOffset();
            UpdateUI();
        }

        private void ApplyOffset()
        {
            if (rhythmManager != null)
            {
                rhythmManager.GlobalOffset = _currentOffset;
            }
        }

        private void SaveOffset()
        {
            PlayerPrefs.SetFloat("RhythmOffset", _currentOffset);
            PlayerPrefs.Save();
            Debug.Log($"<color=green>[Offset]</color> Saved: {_currentOffset * 1000:F0}ms");
        }

        private void UpdateUI()
        {
            if (offsetValueText != null)
            {
                
                offsetValueText.text = $"{_currentOffset * 1000:F0} ms";
            }
        }
    }
}
