using UnityEngine;
using UnityEngine.InputSystem;

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

        // [추가] Canvas GameObject 자체 참조 — Panel만 끄면 Canvas의 GraphicRaycaster가
        //        남아서 클릭 이벤트를 가로채므로 Canvas GameObject도 함께 토글한다.
        [Header("Screen Canvases (Canvas 오브젝트 자체)")]
        [SerializeField, Tooltip("로그인/회원가입 Canvas")] private GameObject _loginRegisterCanvas;
        [SerializeField, Tooltip("로비 Canvas")]           private GameObject _lobbyCanvas;
        [SerializeField, Tooltip("인게임 Overlay Canvas")] private GameObject _inGameOverlayCanvas;
        [SerializeField, Tooltip("인게임 World Space Canvas 목록 (인게임 외에는 비활성화)")]
        private GameObject[] _worldSpaceCanvases;

        [SerializeField] private PlayerInput _playerInput;

        private static UIManager _instance;

        #endregion

        #region Properties

        public static UIManager Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = FindAnyObjectByType<UIManager>();
                }
                return _instance;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // 전용 서버 빌드(헤드리스)에서는 UI가 필요 없으므로 즉시 제거
            if (Application.isBatchMode)
            {
                Destroy(gameObject);
                return;
            }

            if (_instance == null)
            {
                _instance = this;
                // 씬 계층에서 자식으로 배치돼 있어도 DontDestroyOnLoad가 동작하도록 루트로 이동
                if (transform.parent != null)
                    transform.SetParent(null);
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
            // [추가] InputSystemUIInputModule의 Point/Click 액션이 Multiplayer 맵에 있으므로
            //        PlayerInput 초기화 후 맵이 꺼져 있을 경우를 대비해 명시적으로 활성화한다.
            _playerInput?.actions.FindActionMap("Multiplayer", throwIfNotFound: false)?.Enable();
            // [수정] SwitchCurrentActionMap 제거 — Action Map 전환 대신 Hit 액션만 비활성화
            DisableGameActions();
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
            // [수정] 로비도 게임 액션 비활성화 유지
            DisableGameActions();
            SetPanels(login: false, register: false, lobby: true, inGame: false);
        }

        /// <summary>
        /// 인게임 화면으로 전환한다.
        /// </summary>
        public void TransitionToInGame()
        {
            Debug.Log("[UIManager] Transitioning to InGame Screen...");
            // [수정] 인게임 진입 시 Hit 액션만 활성화
            EnableGameActions();
            SetPanels(login: false, register: false, lobby: false, inGame: true);
        }

        #endregion

        #region Private Methods

        private void SetPanels(bool login, bool register, bool lobby, bool inGame)
        {
            // Panel 오브젝트 토글
            if (_loginPanel    != null) _loginPanel.SetActive(login);
            if (_registerPanel != null) _registerPanel.SetActive(register);
            if (_lobbyPanel    != null) _lobbyPanel.SetActive(lobby);
            if (_inGamePanel   != null) _inGamePanel.SetActive(inGame);

            // [추가] Canvas GameObject 토글 — 비활성 Canvas의 GraphicRaycaster가
            //        클릭 이벤트를 가로채지 않도록 Canvas 자체도 켜고 끈다.
            if (_loginRegisterCanvas != null) _loginRegisterCanvas.SetActive(login || register);
            if (_lobbyCanvas         != null) _lobbyCanvas.SetActive(lobby);
            if (_inGameOverlayCanvas != null) _inGameOverlayCanvas.SetActive(inGame);
            if (_worldSpaceCanvases  != null)
                foreach (var canvas in _worldSpaceCanvases)
                    if (canvas != null) canvas.SetActive(inGame);
        }

        // [추가] 로그인/로비에서 게임 입력 액션을 끄고, 인게임 진입 시 다시 켠다.
        //        Action Map 전환 대신 개별 액션 제어를 사용해 InputSystemUIInputModule의
        //        마우스 처리가 영향받지 않도록 한다.
        private void DisableGameActions()
        {
            if (_playerInput == null) return;
            // [수정] [] 인덱서는 미존재 시 KeyNotFoundException 발생 → FindAction으로 교체
            _playerInput.actions.FindAction("Multiplayer/Hit", throwIfNotFound: false)?.Disable();
            _playerInput.actions.FindAction("Keyboard/key1",   throwIfNotFound: false)?.Disable();
            _playerInput.actions.FindAction("Keyboard/key2",   throwIfNotFound: false)?.Disable();
            _playerInput.actions.FindAction("Keyboard/key3",   throwIfNotFound: false)?.Disable();
            _playerInput.actions.FindAction("Keyboard/key4",   throwIfNotFound: false)?.Disable();
        }

        private void EnableGameActions()
        {
            if (_playerInput == null) return;
            // [추가] 멀티 인게임이면 Hit만, 로컬이면 key1~4만 활성화해야 하나
            //        UIManager는 멀티/로컬 구분 없이 인게임 패널 전환만 담당하므로
            //        여기서는 Multiplayer/Hit만 활성화하고 key1~4는 NewRhythmManager에서 관리.
            _playerInput.actions.FindAction("Multiplayer/Hit", throwIfNotFound: false)?.Enable();
        }

        #endregion
    }
}
