using UnityEngine;
using UnityEngine.UI;

namespace BeatDodger.UI
{
    /// <summary>
    /// 게임의 전체적인 UI 화면 전환을 관리하는 중앙 컨트롤러
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        #region Variables

        [Header("Screen Panels")]
        [SerializeField, Tooltip("로그인 화면 패널")] private GameObject _loginPanel;
        [SerializeField, Tooltip("로비 화면 패널")] private GameObject _lobbyPanel;
        [SerializeField, Tooltip("인게임 화면 패널")] private GameObject _inGamePanel;

        private static UIManager _instance;

        #endregion

        #region Properties

        /// <summary>
        /// UIManager의 싱글톤 인스턴스에 접근합니다.
        /// </summary>
        public static UIManager Instance
        {
            get
            {
                return _instance;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }

            TransitionToLogin();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 로비 화면으로 전환한다.
        /// </summary>
        public void TransitionToLobby()
        {
            Debug.Log("[UIManager] Transitioning to Lobby Screen...");

            if (_loginPanel != null)
            {
                _loginPanel.SetActive(false);
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(true);
            }

            if (_inGamePanel != null)
            {
                _inGamePanel.SetActive(false);
            }
        }

        /// <summary>
        /// 로그인 화면으로 전환한다. (로그아웃 시 사용)
        /// </summary>
        public void TransitionToLogin()
        {
            Debug.Log("[UIManager] Transitioning to Login Screen...");

            if (_loginPanel != null)
            {
                _loginPanel.SetActive(true);
            }

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
            }

            if (_inGamePanel != null)
            {
                _inGamePanel.SetActive(false);
            }
        }

        /// <summary>
        /// 인게임 화면으로 전환한다. (방장이 게임 진입 버튼 클릭 시 사용)
        /// </summary>
        public void TransitionToInGame()
        {
            Debug.Log("[UIManager] Transitioning to InGame Screen...");

            if (_lobbyPanel != null)
            {
                _lobbyPanel.SetActive(false);
            }

            if (_loginPanel != null)
            {
                _loginPanel.SetActive(false);
            }

            if (_inGamePanel != null)
            {
                _inGamePanel.SetActive(true);
            }
        }

        #endregion
    }
}
