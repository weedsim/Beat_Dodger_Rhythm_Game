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
        Complete
    }

    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private GameObject volumePanel;
        [SerializeField] private GameObject syncPanel;
        [SerializeField] private GameObject nicknamePanel;

        [Header("Next Buttons")]
        [SerializeField] private Button volumeNextButton;
        [SerializeField] private Button nicknameBackButton;
        [SerializeField] private Button syncNextButton;

        [Header("Volume Controls")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button sfxTestButton;
        [SerializeField] private AudioSource sfxTestAudioSource;
        [SerializeField] private AudioClip sfxTestClip;

        [Header("Nickname Controls")]
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button nicknameConfirmButton;
        [SerializeField] private Button randomNicknameButton;

        [Header("Nickname Check")]
        [SerializeField] private GameObject nicknameCheckPanel;
        [SerializeField] private Button checkYesButton;
        [SerializeField] private Button checkNoButton;

        [Header("Sync Controller")]
        [SerializeField] private BeatDodger.UI.SyncCalibrationController syncController;

        [Header("Transitions")]
        [SerializeField] private CanvasGroup transitionOverlay;
        [SerializeField] private float transitionDuration = 0.5f;

        private readonly string[] _randomPrefixes = { "섹시한", "어지러운", "기운찬", "거대", "로봇", "톡쏘는", "별난", "운좋은", "어지러운", "근사한", "까다로운", "졸린", "고약한", "밤", "운좋은", "엄청난", "무시무시한", "고약한", "엄청난", "행복한", "마법", "별난", "반짝이는", "어지러운", "미친", "고약한", "엄청난", "광포한", "어지러운", "퉁명한", "숨겨진", "번듯한", "광포한", "미친", "엄청난", "재빠른", "운좋은", "엉뚱한", "엄청난", "마법", "막강한", "막강한", "심술쟁이", "게으른", "유령", "까다로운", "로봇", "화끈한", "금빛", "막강한", "행복한", "꼬마", "똑똑한", "숨겨진", "작은", "소리없는", "밤", "퉁명한", "전투", "꼬마", "심술쟁이", "졸린", "재채기하는" };
        private readonly string[] _randomSuffixes = { "드러머", "기타리스트", "키보디스트", "베이시스트" };

        private TutorialStep _currentStep;

        private void Start()
        {
            if (RhythmConfig.Instance == null)
            {
                GameObject configObj = new GameObject("RhythmConfig");
                configObj.AddComponent<RhythmConfig>();
            }
            InitializeTutorial();
        }

        private void InitializeTutorial()
        {
            StartStep(TutorialStep.Volume);
            
            if (nicknameCheckPanel != null) nicknameCheckPanel.SetActive(false);

            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 1f;
                transitionOverlay.DOFade(0f, transitionDuration).OnComplete(() =>
                {
                    transitionOverlay.blocksRaycasts = false;
                });
            }

            // Setup Buttons
            if (volumeNextButton != null) volumeNextButton.onClick.AddListener(NextStep);
            if (nicknameBackButton != null) nicknameBackButton.onClick.AddListener(PreviousStep);
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
            if (sfxTestButton != null) sfxTestButton.onClick.AddListener(PlayTestSFX);

            // Setup Sync
            if (syncController != null) syncController.OnCalibrationComplete.AddListener(OnSyncCalibrationFinished);

            // Setup Nickname
            if (nicknameConfirmButton != null) nicknameConfirmButton.onClick.AddListener(ShowNicknameCheck);
            if (nicknameInput != null) nicknameInput.onSubmit.AddListener((_) => ShowNicknameCheck());
            if (randomNicknameButton != null) randomNicknameButton.onClick.AddListener(GenerateRandomNickname);

            // Setup Nickname Check
            if (checkYesButton != null) checkYesButton.onClick.AddListener(ConfirmAndNext);
            if (checkNoButton != null) checkNoButton.onClick.AddListener(HideNicknameCheck);
        }

        private void StartStep(TutorialStep step)
        {
            _currentStep = step;
            
            if (volumePanel != null) volumePanel.SetActive(step == TutorialStep.Volume);
            if (syncPanel != null) syncPanel.SetActive(step == TutorialStep.Sync);
            if (nicknamePanel != null) nicknamePanel.SetActive(step == TutorialStep.Nickname);

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
                    CompleteTutorial();
                    break;
            }
        }

        private void Update()
        {
            // 보정이 끝나고 다음 버튼이 없을 때 엔터키로 넘어가도록 지원
            if (_currentStep == TutorialStep.Sync && _isCalibrationDone)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    NextStep();
                }
            }
        }

        public void PreviousStep()
        {
            switch (_currentStep)
            {
                case TutorialStep.Sync:
                    StartStep(TutorialStep.Volume);
                    break;
                case TutorialStep.Nickname:
                    StartStep(TutorialStep.Sync);
                    break;
            }
        }

        private void SetBGMVolume(float value) => RhythmConfig.Instance.BGMVolume = value;

        private void SetSFXVolume(float value)
        {
            RhythmConfig.Instance.SFXVolume = value;
            if (sfxTestAudioSource != null) sfxTestAudioSource.volume = value;
        }

        private void PlayTestSFX()
        {
            if (sfxTestAudioSource == null) return;
            if (sfxTestClip != null) sfxTestAudioSource.PlayOneShot(sfxTestClip);
            else sfxTestAudioSource.Play();
        }

        private void GenerateRandomNickname()
        {
            if (nicknameInput == null) return;
            string prefix = _randomPrefixes[Random.Range(0, _randomPrefixes.Length)];
            string suffix = _randomSuffixes[Random.Range(0, _randomSuffixes.Length)];
            nicknameInput.text = $"{prefix} {suffix}";
        }

        private void ShowNicknameCheck()
        {
            if (string.IsNullOrEmpty(nicknameInput.text)) return;
            if (nicknameCheckPanel != null) nicknameCheckPanel.SetActive(true);
            else ConfirmAndNext();
        }

        private void HideNicknameCheck()
        {
            if (nicknameCheckPanel != null) nicknameCheckPanel.SetActive(false);
        }

        private void ConfirmAndNext()
        {
            RhythmConfig.Instance.PlayerNickname = nicknameInput.text;
            if (nicknameCheckPanel != null) nicknameCheckPanel.SetActive(false);
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
                transitionOverlay.DOFade(1f, transitionDuration).OnComplete(() => SceneManager.LoadScene("2.Lobby"));
            }
            else SceneManager.LoadScene("2.Lobby");
        }

        private bool _isCalibrationDone = false;

        public void OnSyncCalibrationFinished()
        {
            if (_currentStep == TutorialStep.Sync)
            {
                _isCalibrationDone = true;
                if (syncNextButton != null)
                {
                    syncNextButton.gameObject.SetActive(true);
                }
            }
        }
    }
}
