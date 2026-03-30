using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BeatDodger.UI
{
    /// <summary>
    /// 로그인 시퀀스 및 씬 전환을 관리하는 매니저입니다.
    /// [화면 클릭 -> 로그인창 ON] -> [로그인 버튼 -> 로그인창 OFF] -> [화면 클릭 -> 씬 이동]
    /// </summary>
    public sealed class LoginFlowManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _screenOverlayButton; // 화면 전체 클릭 감지용 투명 버튼

        [Header("Scene Settings")]
        [SerializeField] private string _nextSceneName = "MainGameScene";

        private enum FlowState { WaitToLogin, InLogin, WaitToMove }
        private FlowState _currentState = FlowState.WaitToLogin;

        private void Awake()
        {
            // 초기 상태 설정
            if (_loginPanel != null) _loginPanel.SetActive(false);

            // 이벤트 리스너 연결
            if (_screenOverlayButton != null)
            {
                _screenOverlayButton.onClick.AddListener(OnScreenClicked);
            }

            if (_loginButton != null)
            {
                _loginButton.onClick.AddListener(OnLoginButtonClicked);
            }
        }

        private void OnScreenClicked()
        {
            switch (_currentState)
            {
                case FlowState.WaitToLogin:
                    // 1단계: 화면 클릭 시 로그인 패널 활성
                    ShowLoginPanel();
                    break;

                case FlowState.WaitToMove:
                    // 3단계: 로그인 후 다시 화면 클릭 시 씬 이동
                    MoveToNextScene();
                    break;
            }
        }

        private void OnLoginButtonClicked()
        {
            if (_currentState == FlowState.InLogin)
            {
                // 2단계: 로그인 버튼 클릭 시 패널 비활성화 및 다음 상태로 전환
                HideLoginPanel();
                _currentState = FlowState.WaitToMove;
            }
        }

        private void ShowLoginPanel()
        {
            if (_loginPanel == null) return;

            _loginPanel.SetActive(true);
            _currentState = FlowState.InLogin;
        }

        private void HideLoginPanel()
        {
            if (_loginPanel == null) return;

            _loginPanel.SetActive(false);
        }

        private void MoveToNextScene()
        {
            if (string.IsNullOrEmpty(_nextSceneName)) return;

            // 씬 캐싱이나 복잡한 로직 없이 즉시 이동 (유니티 기본 방식)
            SceneManager.LoadScene(_nextSceneName);
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위해 리스너 제거
            _screenOverlayButton?.onClick.RemoveAllListeners();
            _loginButton?.onClick.RemoveAllListeners();
        }
    }
}
