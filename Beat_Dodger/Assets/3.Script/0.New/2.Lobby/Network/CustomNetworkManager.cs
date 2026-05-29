using UnityEngine;
using Mirror;
using BeatDodger.Core;
using BeatDodger.Services;
using BeatDodger.Managers;
using BeatDodger.Network;
using BeatDodger.Database;
using BeatDodger.Lobby;
using BeatDodger.Sessions;
using BeatDodger.UI;

namespace BeatDodger.Network
{
    /// <summary>
    /// Mirror NetworkManager 오버라이드. 각 책임은 전담 핸들러에 위임한다.
    /// </summary>
    public class CustomNetworkManager : NetworkManager, ILoginEntryPoint, IServerNetworkHandler
    {
        #region Variables

        private IDBService _dbService;
        private ILoginHandler _loginHandler;
        private IPlayerSpawner _playerSpawner;

        [Header("Server Side References")]
        [SerializeField]
        private SessionCoordinator _sessionCoordinator;

        [SerializeField, Tooltip("서버 컴포넌트 팩토리. 미할당 시 기본 구현체를 사용한다.")]
        private ServerComponentFactory _factory;

        [SerializeField, Tooltip("파티 네트워크 브릿지 (서버에서 PartyNetworkBridge와 연결)")]
        private PartyNetworkBridge _partyNetworkBridge;

#if !UNITY_SERVER
        [Header("Client Side References")]
        [SerializeField]
        private ClientPlayerManager _clientPlayerManager;

        [Header("UI References")]
        [SerializeField] private UIManager _uiManager;
#endif

        private IClientUIBridge _uiBridge;
        private IClientAuthFlow _clientAuthFlow;

        #endregion

        #region Unity Lifecycle

        public override void Awake()
        {
            base.Awake();

            if (_factory == null)
            {
                _factory = ScriptableObject.CreateInstance<ServerComponentFactory>();
            }

            if (NetworkServer.active || IsServerBuild())
            {
                InitializeServerComponents();
            }

#if !UNITY_SERVER
            if (_clientPlayerManager == null)
            {
                if (!TryGetComponent(out _clientPlayerManager))
                {
                    Debug.LogError("[CustomNetworkManager] [Client] ClientPlayerManager component is missing!");
                }
            }

            if (_uiManager == null)
            {
                _uiManager = UIManager.Instance;
                if(_uiManager == null)
                {
                    Debug.LogError("[CustomNetworkManager] [Client] UIManager reference is missing!");
                }
            }

            if (_clientPlayerManager != null)
            {
                _uiBridge = _uiManager;
                _clientAuthFlow = new ClientAuthFlow(_clientPlayerManager, _uiBridge);
            }
#endif

            autoCreatePlayer = false;
        }

        public override void Start()
        {
            Debug.Log("[CustomNetworkManager] Start");
            base.Start();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 로그인 UI에서 호출. 인증 흐름에 로그인 정보를 전달하고 접속을 시작한다.
        /// </summary>
        public void AttemptLogin(string userId, string password)
        {
#if !UNITY_SERVER
            _clientAuthFlow.PrepareLogin(userId, password);
            Debug.Log($"[CustomNetworkManager] [Client] Attempting to connect for user: {userId}");
            StartClient();
#endif
        }

        /// <summary>
        /// 회원가입 UI에서 호출. 인증 흐름에 회원가입 정보를 전달하고 접속을 시작한다.
        /// </summary>
        public void AttemptRegister(string userId, string password, string userName)
        {
#if !UNITY_SERVER
            _clientAuthFlow.PrepareRegister(userId, password, userName);
            Debug.Log($"[CustomNetworkManager] [Client] Attempting to register user: {userId}");
            StartClient();
#endif
        }

        public override void OnClientConnect()
        {
#if !UNITY_SERVER
            base.OnClientConnect();
            Debug.Log("[CustomNetworkManager] [Client] Connected to server. Starting custom auth flow...");
            _clientAuthFlow.OnConnected();
#endif
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            if (!IsServerRunning())
            {
                return;
            }

            string tempUserId = $"temp_{conn.connectionId}";
            _sessionCoordinator.RegisterPlayer(conn, tempUserId);

            Debug.Log($"[CustomNetworkManager] [Server] Player connected. Session registered: {tempUserId}");
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (!IsServerRunning())
            {
                return;
            }

            if (_sessionCoordinator.GetPlayerState(conn) != SessionState.Lobby)
            {
                Debug.LogWarning($"[CustomNetworkManager] [Server] OnServerAddPlayer rejected: conn {conn.connectionId} is not in Lobby state.");
                return;
            }

            _playerSpawner.SpawnPlayer(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (IsServerRunning())
            {
                uint disconnectedNetId = _sessionCoordinator.GetNetIdByConnection(conn);

                if (disconnectedNetId == 0 && conn.identity != null)
                {
                    disconnectedNetId = conn.identity.netId;
                }

                if (_partyNetworkBridge != null && disconnectedNetId != 0)
                {
                    _partyNetworkBridge.HandlePlayerDisconnect(disconnectedNetId);
                }

                _sessionCoordinator.UnregisterPlayer(conn);

                Debug.Log("[CustomNetworkManager] [Server] Player disconnected. Session cleaned up.");
            }

            base.OnServerDisconnect(conn);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            Debug.Log("[CustomNetworkManager] [Server] Dedicated Server has started successfully.");

            InitializeServerComponents();
            RegisterServerHandlers();

            if (_sessionCoordinator == null)
            {
                Debug.LogError("[CustomNetworkManager] SessionCoordinator is missing!");
                return;
            }

            Debug.Log("[CustomNetworkManager] [Server] All systems ready. Waiting for clients...");
        }

        #endregion

        #region Private Methods

        private bool IsServerBuild()
        {
#if UNITY_SERVER
            return true;
#else
            return false;
#endif
        }

        private bool IsServerRunning()
        {
            return NetworkServer.active;
        }

        private void InitializeServerComponents()
        {
            if (_dbService == null)
            {
                _dbService = _factory.CreateDBService();
            }

            if (_sessionCoordinator == null)
            {
                if (!TryGetComponent(out _sessionCoordinator))
                {
                    Debug.LogError("[CustomNetworkManager] [Server] SessionCoordinator is missing!");
                }
            }

            if (_loginHandler == null && _dbService != null && _sessionCoordinator != null)
            {
                _loginHandler = _factory.CreateLoginHandler(_dbService, _sessionCoordinator);
            }

            if (_playerSpawner == null && _sessionCoordinator != null)
            {
                _playerSpawner = _factory.CreatePlayerSpawner(playerPrefab, _sessionCoordinator);
            }
        }

        private void RegisterServerHandlers()
        {
            NetworkServer.RegisterHandler<LoginRequestMessage>(OnLoginRequest);
            NetworkServer.RegisterHandler<RegisterRequestMessage>(OnRegisterRequest);
        }

        private void OnLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            if (!IsServerRunning())
            {
                return;
            }

            _loginHandler.HandleLoginRequest(conn, msg);
        }

        private void OnRegisterRequest(NetworkConnectionToClient conn, RegisterRequestMessage msg)
        {
            if (!IsServerRunning())
            {
                return;
            }

            _loginHandler.HandleRegisterRequest(conn, msg);
        }

        #endregion
    }
}
