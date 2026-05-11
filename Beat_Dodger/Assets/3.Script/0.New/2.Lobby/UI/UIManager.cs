using UnityEngine;
using UnityEngine.UI;

namespace BeatDodger.UI
{
    /// <summary>
    /// 게임의 전체적인 UI 화면 전환을 관리하는 중앙 컨트롤러
    /// </summary>
    public class UIManager : MonoBehaviour, IClientUIBridge
    {
        #region Variables

        [Header("Screen Panels")]
        [SerializeField, Tooltip("로그인 화면 패널")]      private GameObject _loginPanel;
        [SerializeField, Tooltip("회원가입 화면 패널")]    private GameObject _registerPanel;
        [SerializeField, Tooltip("로비 화면 패널")]        private GameObject _lobbyPanel;
        [SerializeField, Tooltip("인게임 화면 패널")]      private GameObject _inGamePanel;

        private static UIManager _instance;

        #endregion

        #region Properties

        public static UIManager Instance => _instance;

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
                return;
            }

            TransitionToLogin();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 로그인 패널로 전환한다. (회원가입 패널 → 로그인 패널, 또는 로그아웃 시 사용)
        /// </summary>
        public void TransitionToLogin()
        {
            Debug.Log("[UIManager] Transitioning to Login Panel...");
            SetPanels(login: true, register: false, lobby: false, inGame: false);
        }

        /// <summary>
        /// 회원가입 패널로 전환한다. (로그인 패널 → 회원가입 패널)
        /// </summary>
        public void TransitionToRegister()
        {
            Debug.Log("[UIManager] Transitioning to Register Panel...");
            SetPanels(login: false, register: true, lobby: false, inGame: false);
        }

        /// <summary>
        /// 로비 화면으로 전환한다.
        /// </summary>
        public void TransitionToLobby()
        {
            Debug.Log("[UIManager] Transitioning to Lobby Screen...");
            SetPanels(login: false, register: false, lobby: true, inGame: false);
        }

        /// <summary>
        /// 인게임 화면으로 전환한다.
        /// </summary>
        public void TransitionToInGame()
        {
            Debug.Log("[UIManager] Transitioning to InGame Screen...");
            SetPanels(login: false, register: false, lobby: false, inGame: true);
        }

        #endregion

        #region Private Methods

        private void SetPanels(bool login, bool register, bool lobby, bool inGame)
        {
            if (_loginPanel    != null) _loginPanel.SetActive(login);
            if (_registerPanel != null) _registerPanel.SetActive(register);
            if (_lobbyPanel    != null) _lobbyPanel.SetActive(lobby);
            if (_inGamePanel   != null) _inGamePanel.SetActive(inGame);
        }

        #endregion
    }
}
