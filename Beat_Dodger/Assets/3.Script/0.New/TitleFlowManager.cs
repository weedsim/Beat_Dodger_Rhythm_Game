using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace BeatDodger.UI
{
    /// <summary>
    /// TitleFlowManager handles the specific sequence:
    /// 1. Fade In -> 2. Click Screen -> 3. Show Login -> 4. Click Login -> 5. Hide Login -> 6. Click Screen -> 7. Fade Out -> 8. Load Scene
    /// </summary>
    public sealed class TitleFlowManager : MonoBehaviour
    {
        [Header("UI Panels & Groups")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private CanvasGroup titleContentGroup; 
        [SerializeField] private CanvasGroup transitionOverlay;

        [Header("Interactive Elements")]
        [SerializeField] private Button screenOverlayButton; 
        [SerializeField] private Button loginButton;

        [Header("Transition Settings")]
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private string tutorialSceneName = "Tutorial";
        [SerializeField] private string lobbySceneName = "2.Lobby";

        private enum State { WaitingForLogin, InLogin, WaitingForTransition, Transitioning }
        private State _currentState = State.WaitingForLogin;

        private void Awake()
        {
            InitializeUI();
            SetupButtonListeners();
        }

        private void Start()
        {
            // Start BGM
            if (RhythmConfig.Instance != null)
            {
                RhythmConfig.Instance.PlayTitleBGM();
            }

            // 1. TransitionOverray 트랜지션 (Fade In)
            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 1f;
                transitionOverlay.blocksRaycasts = true;
                transitionOverlay.DOFade(0f, transitionDuration).OnComplete(() =>
                {
                    transitionOverlay.blocksRaycasts = false;
                    Debug.Log("[Title] Fade In Complete. State: WaitingForLogin");
                });
            }
        }

        private void InitializeUI()
        {
            if (loginPanel != null) loginPanel.SetActive(false);
            
            if (titleContentGroup != null)
            {
                titleContentGroup.alpha = 1f;
                titleContentGroup.interactable = true;
                titleContentGroup.blocksRaycasts = true;
                titleContentGroup.gameObject.SetActive(true);
            }
        }

        private void SetupButtonListeners()
        {
            if (screenOverlayButton != null)
            {
                screenOverlayButton.onClick.RemoveAllListeners();
                screenOverlayButton.onClick.AddListener(OnScreenClicked);
            }

            if (loginButton != null)
            {
                loginButton.onClick.RemoveAllListeners();
                loginButton.onClick.AddListener(OnLoginButtonClicked);
            }
        }

        private void OnScreenClicked()
        {
            Debug.Log($"[Title] Screen Clicked! Current State: {_currentState}");
            
            switch (_currentState)
            {
                case State.WaitingForLogin:
                    ProceedToNextScene();
                    break;

                case State.WaitingForTransition:
                    ProceedToNextScene();
                    break;
            }
        }

        private void ShowLoginPanel()
        {
            Debug.Log("[Title] Showing Login Panel.");
            if (loginPanel == null)
            {
                _currentState = State.WaitingForTransition;
                return;
            }

            loginPanel.SetActive(true);
            _currentState = State.InLogin;

            // Optional: Dim the title content while logging in
            if (titleContentGroup != null) titleContentGroup.alpha = 0.5f;
            
            // Disable screen button to ensure only login button can be clicked
            if (screenOverlayButton != null) screenOverlayButton.interactable = false;
        }

        private void OnLoginButtonClicked()
        {
            if (_currentState != State.InLogin) return;

            Debug.Log("[Title] Login Button Clicked.");

            // 4. Login Button 클릭 -> 5. Login Panel 비활성화
            if (loginPanel != null) loginPanel.SetActive(false);
            
            _currentState = State.WaitingForTransition;

            // Restore title content and re-enable screen button
            if (titleContentGroup != null) titleContentGroup.alpha = 1f;
            if (screenOverlayButton != null) screenOverlayButton.interactable = true;
            
            Debug.Log("[Title] Login Panel hidden. Waiting for final click to transition.");
        }

        private void ProceedToNextScene()
        {
            if (_currentState == State.Transitioning) return;
            _currentState = State.Transitioning;

            // 7. TransitionOverray 트랜지션 (Fade Out) -> 8. 씬이동
            bool isFirstTime = PlayerPrefs.GetInt("IsFirstTime", 1) == 1;
            string nextScene = isFirstTime ? tutorialSceneName : lobbySceneName;

            Debug.Log($"[Title] Final click detected. Transitioning to {nextScene}.");

            if (transitionOverlay != null)
            {
                transitionOverlay.blocksRaycasts = true;
                transitionOverlay.DOFade(1f, transitionDuration).OnComplete(() =>
                {
                    SceneManager.LoadScene(nextScene);
                });
            }
            else
            {
                SceneManager.LoadScene(nextScene);
            }
        }
    }
}
