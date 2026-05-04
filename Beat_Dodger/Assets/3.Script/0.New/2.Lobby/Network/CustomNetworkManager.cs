using System.Collections.Generic;
using UnityEngine;
using Mirror;
using BeatDodger.Core;
using BeatDodger.Services;
using BeatDodger.Managers;
using BeatDodger.Network;
using BeatDodger.Database;
using BeatDodger.Lobby;
using BeatDodger.Sessions;

namespace BeatDodger.Network
{
    /// <summary>
    /// 비트 발키리 프로젝트의 메인 네트워크 관리자.
    /// DB 서비스와 세션 코디네이터를 통합하여 서버/클라이언트의 흐름을 제어한다.
    /// </summary>
    public class CustomNetworkManager : NetworkManager
    {
        #region Variables

        private DBService _dbService;
        private ILoginHandler _loginHandler;

        [Header("Server Side References")]
        [SerializeField]
        private SessionCoordinator _sessionCoordinator;

        [SerializeField, Tooltip("파티 네트워크 브릿지 (서버에서 PartyNetworkBridge와 연결)")]
        private PartyNetworkBridge _partyNetworkBridge;

#if !UNITY_SERVER
        [Header("Client Side References")]
        [SerializeField]
        private ClientPlayerManager _clientPlayerManager;

        [Header("UI References")]
        [SerializeField] private BeatDodger.UI.UIManager _uiManager;
#endif

        private string _pendingUserId;
        private string _pendingPassword;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// NetworkManager 초기화. DB 서비스, 로그인 핸들러, 컴포넌트 참조를 설정한다.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            // C1: 런타임 체크로 서버/클라이언트 분기 — #if UNITY_SERVER 대신 사용
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
#endif

            autoCreatePlayer = false;
        }

        /// <summary>
        /// 핸들러 등록. 서버/클라이언트 각각의 메시지 핸들러를 등록한다.
        /// </summary>
        public override void Start()
        {
            Debug.Log("[CustomNetworkManager] Start");
            base.Start();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 로그인 UI에서 호출하는 메서드. 정보를 캐싱하고 접속을 시작한다.
        /// </summary>
        /// <param name="userId">입력받은 유저 ID</param>
        /// <param name="password">입력받은 비밀번호</param>
        public void AttemptLogin(string userId, string password)
        {
#if !UNITY_SERVER
            _pendingUserId = userId;
            _pendingPassword = password;

            Debug.Log($"[CustomNetworkManager] [Client] Attempting to connect for user: {userId}");
            StartClient();
#endif
        }

        /// <summary>
        /// 클라이언트가 서버와 연결에 성공했을 때 호출된다.
        /// </summary>
        public override void OnClientConnect()
        {
#if !UNITY_SERVER
            base.OnClientConnect();

            Debug.Log("[CustomNetworkManager] [Client] Connected to server. Starting custom auth flow...");

            RegisterClientHandlers();

            if (!string.IsNullOrEmpty(_pendingUserId))
            {
                LoginRequestMessage loginMsg = new LoginRequestMessage
                {
                    UserId = _pendingUserId,
                    Password = _pendingPassword
                };

                NetworkClient.Send(loginMsg);

                _pendingUserId = null;
                _pendingPassword = null;

                Debug.Log("[CustomNetworkManager] [Client] Connected. Sent login request to server.");
            }
#endif
        }

        /// <summary>
        /// 클라이언트가 서버에 연결되었을 때 호출된다.
        /// </summary>
        /// <param name="conn">접속한 클라이언트의 네트워크 연결</param>
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

        /// <summary>
        /// 클라이언트가 AddPlayer 메시지를 전송하면 서버에서 호출된다.
        /// SessionState.Lobby 상태인 연결에 한해 LobbyPlayer 를 스폰하고 세션에 netId 를 등록한다.
        /// </summary>
        /// <param name="conn">AddPlayer 를 요청한 클라이언트의 네트워크 연결</param>
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

            GameObject playerGo = Instantiate(playerPrefab);
            NetworkServer.AddPlayerForConnection(conn, playerGo);

            if (playerGo.TryGetComponent(out LobbyPlayer lobbyPlayer))
            {
                lobbyPlayer.SetIdentity(GetUserIdFromSession(conn), GetUserNameFromSession(conn));
            }

            _sessionCoordinator.SetPlayerNetId(conn, conn.identity.netId);

            Debug.Log($"[CustomNetworkManager] [Server] LobbyPlayer spawned for conn {conn.connectionId} | netId: {conn.identity.netId}");
        }

        /// <summary>
        /// 클라이언트가 서버와 연결이 끊겼을 때 호출된다.
        /// </summary>
        /// <param name="conn">연결이 끊긴 클라이언트의 네트워크 연결</param>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (IsServerRunning())
            {
                // netId 를 UnregisterPlayer 호출 전에 취득해야 매핑이 살아 있음
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

        /// <summary>
        /// 서버가 성공적으로 시작되었을 때 호출되는 콜백.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            Debug.Log("[CustomNetworkManager] [Server] Dedicated Server has started successfully.");

            // 서버 시작 시점에 컴포넌트 재확인 (Awake보다 늦게 active가 확정될 수 있음)
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
                _dbService = new DBService(new DBRepository());
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
                _loginHandler = new LoginHandler(_dbService, _sessionCoordinator);
            }
        }

        private void RegisterServerHandlers()
        {
            NetworkServer.RegisterHandler<LoginRequestMessage>(OnLoginRequest);
        }

        private void OnLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            if (!IsServerRunning())
            {
                return;
            }

            // C4: async void 제거 — ILoginHandler.HandleLoginRequest 내부에서 Task 발행
            _loginHandler.HandleLoginRequest(conn, msg);
        }

        private void RegisterClientHandlers()
        {
            NetworkClient.RegisterHandler<LoginResponseMessage>(OnLoginResponse);
            NetworkClient.RegisterHandler<EnterGameMessage>(OnEnterGameMessage);
        }

        private void OnLoginResponse(LoginResponseMessage msg)
        {
#if !UNITY_SERVER
            if (msg.Success)
            {
                Debug.Log($"[CustomNetworkManager] [Client] Login Successful! Welcome, UID: {msg.UserId} | UserName: {msg.UserName}");

                if (_clientPlayerManager != null)
                {
                    UserData userData = new UserData
                    {
                        _UserId = msg.UserId,
                        _UserName = msg.UserName,
                        _Level = msg.Level,
                        _Currency = msg.Currency
                    };
                    _clientPlayerManager.SetUserData(userData);
                }

                if ((_uiManager = BeatDodger.UI.UIManager.Instance) != null)
                {
                    _uiManager.TransitionToLobby();
                }

                // 서버의 OnServerAddPlayer 를 트리거하여 LobbyPlayer 를 스폰한다.
                NetworkClient.AddPlayer();
            }
            else
            {
                Debug.LogError("[CustomNetworkManager] [Client] Login Failed. Invalid ID or Password.");
                // TODO: UI에 "로그인 실패" 팝업 표시
            }
#endif
        }

        private void OnEnterGameMessage(EnterGameMessage msg)
        {
#if !UNITY_SERVER
            Debug.Log($"[CustomNetworkManager] [Client] EnterGameMessage 수신 | MatchId: {msg.MatchId} | Difficulty: {msg.Difficulty}");

            if ((_uiManager = BeatDodger.UI.UIManager.Instance) != null)
            {
                _uiManager.TransitionToInGame();
            }
#endif
        }

        private string GetUserIdFromSession(NetworkConnectionToClient conn)
        {
            if (_sessionCoordinator == null)
            {
                return string.Empty;
            }

            PlayerSessionInfo session = _sessionCoordinator.GetPlayerSession(conn);

            if (session != null)
            {
                return session.UserId;
            }

            return string.Empty;
        }

        private string GetUserNameFromSession(NetworkConnectionToClient conn)
        {
            if (_sessionCoordinator == null)
            {
                return string.Empty;
            }

            PlayerSessionInfo session = _sessionCoordinator.GetPlayerSession(conn);

            if (session != null)
            {
                return session.UserName;
            }

            return string.Empty;
        }

        #endregion
    }
}
