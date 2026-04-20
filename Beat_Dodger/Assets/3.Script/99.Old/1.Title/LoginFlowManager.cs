using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BeatDodger.UI
{
    
    
    
    
    public sealed class LoginFlowManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _screenOverlayButton; 

        [Header("Scene Settings")]
        [SerializeField] private string _nextSceneName = "MainGameScene";

        private enum FlowState { WaitToLogin, InLogin, WaitToMove }
        private FlowState _currentState = FlowState.WaitToLogin;

        private void Awake()
        {
            
            if (_loginPanel != null) _loginPanel.SetActive(false);

            
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
                    
                    ShowLoginPanel();
                    break;

                case FlowState.WaitToMove:
                    
                    MoveToNextScene();
                    break;
            }
        }

        private void OnLoginButtonClicked()
        {
            if (_currentState == FlowState.InLogin)
            {
                
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

            
            SceneManager.LoadScene(_nextSceneName);
        }

        private void OnDestroy()
        {
            
            _screenOverlayButton?.onClick.RemoveAllListeners();
            _loginButton?.onClick.RemoveAllListeners();
        }
    }
}
