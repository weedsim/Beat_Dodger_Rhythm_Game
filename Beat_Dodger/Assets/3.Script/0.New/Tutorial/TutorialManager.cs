using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.SceneManagement;

namespace BeatDodger.Tutorial
{
    public enum TutorialStep
    {
        Volume,
        Sync,
        Nickname,
        TutorialGameplay,
        Complete
    }

    public class TutorialManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject volumePanel;
        [SerializeField] private GameObject syncPanel;
        [SerializeField] private GameObject nicknamePanel;
        [SerializeField] private GameObject gameplayTutorialPanel;

        [Header("Next Buttons")]
        [SerializeField] private Button volumeNextButton;
        [SerializeField] private Button syncNextButton;

        [Header("Volume Controls")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Nickname Controls")]
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button nicknameConfirmButton;

        [Header("Sync Controller")]
        [SerializeField] private BeatDodger.UI.SyncCalibrationController syncController;

        [Header("Transitions")]
        [SerializeField] private CanvasGroup transitionOverlay;
        [SerializeField] private float transitionDuration = 0.5f;

        private TutorialStep _currentStep;

        private void Start()
        {
            InitializeTutorial();
        }

        private void InitializeTutorial()
        {
            // Hide all panels initially
            volumePanel.SetActive(false);
            syncPanel.SetActive(false);
            nicknamePanel.SetActive(false);
            gameplayTutorialPanel.SetActive(false);

            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 1f;
                transitionOverlay.DOFade(0f, transitionDuration).OnComplete(() =>
                {
                    transitionOverlay.blocksRaycasts = false;
                    StartStep(TutorialStep.Volume);
                });
            }
            else
            {
                StartStep(TutorialStep.Volume);
            }

            // Setup Buttons
            if (volumeNextButton != null) volumeNextButton.onClick.AddListener(NextStep);
            if (syncNextButton != null)
            {
                syncNextButton.onClick.AddListener(NextStep);
                syncNextButton.gameObject.SetActive(false);
            }

            // Setup Volume Sliders
            if (bgmSlider != null)
            {
                bgmSlider.value = RhythmConfig.Instance.BGMVolume;
                bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            }
            if (sfxSlider != null)
            {
                sfxSlider.value = RhythmConfig.Instance.SFXVolume;
                sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            }

            // Setup Sync
            if (syncController != null)
            {
                syncController.OnCalibrationComplete.AddListener(OnSyncCalibrationFinished);
            }

            // Setup Nickname
            if (nicknameConfirmButton != null)
            {
                nicknameConfirmButton.onClick.AddListener(ConfirmNickname);
            }
        }

        private void StartStep(TutorialStep step)
        {
            _currentStep = step;
            
            // Deactivate all first (or handle transitions)
            volumePanel.SetActive(step == TutorialStep.Volume);
            syncPanel.SetActive(step == TutorialStep.Sync);
            nicknamePanel.SetActive(step == TutorialStep.Nickname);
            gameplayTutorialPanel.SetActive(step == TutorialStep.TutorialGameplay);

            if (step == TutorialStep.Sync && syncController != null)
            {
                syncController.StartCalibration();
            }
        }

        public void NextStep()
        {
            switch (_currentStep)
            {
                case TutorialStep.Volume:
                    StartStep(TutorialStep.Sync);
                    break;
                case TutorialStep.Sync:
                    if (syncController != null) syncController.StopCalibration();
                    StartStep(TutorialStep.Nickname);
                    break;
                case TutorialStep.Nickname:
                    StartStep(TutorialStep.TutorialGameplay);
                    break;
                case TutorialStep.TutorialGameplay:
                    CompleteTutorial();
                    break;
            }
        }

        private void SetBGMVolume(float value)
        {
            RhythmConfig.Instance.BGMVolume = value;
            // TODO: Update actual AudioMixer or AudioSource
        }

        private void SetSFXVolume(float value)
        {
            RhythmConfig.Instance.SFXVolume = value;
            // TODO: Update actual AudioMixer or AudioSource
        }

        private void ConfirmNickname()
        {
            if (string.IsNullOrEmpty(nicknameInput.text)) return;
            
            RhythmConfig.Instance.PlayerNickname = nicknameInput.text;
            NextStep();
        }

        private void CompleteTutorial()
        {
            RhythmConfig.Instance.SaveSettings();
            PlayerPrefs.SetInt("IsFirstTime", 0);
            PlayerPrefs.Save();
            
            if (transitionOverlay != null)
            {
                transitionOverlay.blocksRaycasts = true;
                transitionOverlay.DOFade(1f, transitionDuration).OnComplete(() =>
                {
                    SceneManager.LoadScene("2.Lobby");
                });
            }
            else
            {
                SceneManager.LoadScene("2.Lobby");
            }
        }

        // Called by UI buttons or SyncController when finished
        public void OnSyncCalibrationFinished()
        {
            if (_currentStep == TutorialStep.Sync)
            {
                if (syncNextButton != null)
                {
                    syncNextButton.gameObject.SetActive(true);
                }
                else
                {
                    // If no next button provided, auto-progress
                    NextStep();
                }
            }
        }
    }
}
