using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace BeatDodger.UI
{
    /// <summary>
    /// Manager class that handles Settings and Sync Calibration UI.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject calibrationPanel;

        [Header("Transition Settings")]
        [SerializeField] private CanvasGroup transitionOverlay;
        [SerializeField] private float transitionDuration = 0.5f;

        [Header("Calibration Controller")]
        [SerializeField] private SyncCalibrationController calibrationController;

        private bool _isSettingsOpen;
        private bool _isCalibrationActive;

        private void Start()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (calibrationPanel != null) calibrationPanel.SetActive(false);
            
            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 0f;
                transitionOverlay.blocksRaycasts = false;
            }
        }

        public void ToggleSettings()
        {
            if (_isCalibrationActive) return;

            _isSettingsOpen = !_isSettingsOpen;
            
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(_isSettingsOpen);
            }

            // Pause game when settings are open
            Time.timeScale = _isSettingsOpen ? 0f : 1f;
        }

        /// <summary>
        /// Transition to Calibration page.
        /// </summary>
        public void MoveToCalibration()
        {
            if (transitionOverlay == null)
            {
                SwitchToCalibrationDirect();
                return;
            }

            _isCalibrationActive = true;
            transitionOverlay.blocksRaycasts = true;
            
            // Fade Out -> Switch Panels -> Fade In
            transitionOverlay.DOFade(1f, transitionDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (settingsPanel != null) settingsPanel.SetActive(false);
                    if (calibrationPanel != null) calibrationPanel.SetActive(true);

                    if (calibrationController != null)
                    {
                        calibrationController.StartCalibration();
                    }

                    transitionOverlay.DOFade(0f, transitionDuration)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            transitionOverlay.blocksRaycasts = false;
                        });
                });
        }

        /// <summary>
        /// Return to Settings from Calibration page.
        /// </summary>
        public void BackToSettings()
        {
            if (transitionOverlay == null)
            {
                SwitchToSettingsDirect();
                return;
            }

            transitionOverlay.blocksRaycasts = true;
            
            transitionOverlay.DOFade(1f, transitionDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (calibrationController != null)
                    {
                        calibrationController.StopCalibration();
                    }

                    if (calibrationPanel != null) calibrationPanel.SetActive(false);
                    if (settingsPanel != null) settingsPanel.SetActive(true);
                    _isCalibrationActive = false;

                    transitionOverlay.DOFade(0f, transitionDuration)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            transitionOverlay.blocksRaycasts = false;
                        });
                });
        }

        private void SwitchToCalibrationDirect()
        {
            _isCalibrationActive = true;
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (calibrationPanel != null) calibrationPanel.SetActive(true);
        }

        private void SwitchToSettingsDirect()
        {
            _isCalibrationActive = false;
            if (calibrationPanel != null) calibrationPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }
    }
}
