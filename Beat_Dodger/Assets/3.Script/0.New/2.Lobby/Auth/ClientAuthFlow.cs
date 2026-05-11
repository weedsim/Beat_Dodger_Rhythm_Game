using Mirror;
using UnityEngine;
using BeatDodger.Core;
using BeatDodger.Managers;
using BeatDodger.UI;

namespace BeatDodger.Network
{
    /// <summary>
    /// 클라이언트 인증 흐름 전담 클래스.
    /// 로그인/회원가입 요청 전송, 응답 처리, UI 전환을 책임진다.
    /// </summary>
    public class ClientAuthFlow : IClientAuthFlow
    {
        #region Variables

        private enum PendingMode { None, Login, Register }

        private PendingMode _pendingMode = PendingMode.None;
        private string _pendingUserId;
        private string _pendingPassword;
        private string _pendingUserName;

        private readonly ClientPlayerManager _clientPlayerManager;
        private readonly IClientUIBridge _uiBridge;

        #endregion

        #region Constructor

        public ClientAuthFlow(ClientPlayerManager clientPlayerManager, IClientUIBridge uiBridge)
        {
            _clientPlayerManager = clientPlayerManager;
            _uiBridge = uiBridge;
        }

        #endregion

        #region Public Methods

        public void PrepareLogin(string userId, string password)
        {
            _pendingMode     = PendingMode.Login;
            _pendingUserId   = userId;
            _pendingPassword = password;
        }

        public void PrepareRegister(string userId, string password, string userName)
        {
            _pendingMode     = PendingMode.Register;
            _pendingUserId   = userId;
            _pendingPassword = password;
            _pendingUserName = userName;
        }

        public void OnConnected()
        {
            NetworkClient.RegisterHandler<LoginResponseMessage>(OnLoginResponse);
            NetworkClient.RegisterHandler<RegisterResponseMessage>(OnRegisterResponse);
            NetworkClient.RegisterHandler<EnterGameMessage>(OnEnterGameMessage);

            switch (_pendingMode)
            {
                case PendingMode.Login:
                    NetworkClient.Send(new LoginRequestMessage
                    {
                        UserId   = _pendingUserId,
                        Password = _pendingPassword
                    });
                    Debug.Log("[ClientAuthFlow] [Client] 로그인 요청 전송.");
                    break;

                case PendingMode.Register:
                    NetworkClient.Send(new RegisterRequestMessage
                    {
                        UserId      = _pendingUserId,
                        Password    = _pendingPassword,
                        TempNickName = _pendingUserName
                    });
                    Debug.Log("[ClientAuthFlow] [Client] 회원가입 요청 전송.");
                    break;
            }

            ClearPendingData();
        }

        #endregion

        #region Private Methods

        private void OnLoginResponse(LoginResponseMessage msg)
        {
            if (msg.Success)
            {
                Debug.Log($"[ClientAuthFlow] [Client] 로그인 성공! UID: {msg.UserId} | UserName: {msg.UserName}");

                _clientPlayerManager?.SetUserData(new UserData
                {
                    _UserId    = msg.UserId,
                    _UserName  = msg.UserName,
                    _Volume    = msg.Volume,
                    _Sync      = msg.Sync,
                    _InputKey  = msg.InputKey,
                    _FrameRate = msg.FrameRate
                });

                _uiBridge?.TransitionToLobby();
                NetworkClient.AddPlayer();
            }
            else
            {
                Debug.LogError("[ClientAuthFlow] [Client] 로그인 실패. 아이디 또는 비밀번호가 올바르지 않습니다.");
            }
        }

        private void OnRegisterResponse(RegisterResponseMessage msg)
        {
            if (msg.Success)
            {
                Debug.Log($"[ClientAuthFlow] [Client] 회원가입 성공: {msg.Message}");
                _uiBridge?.TransitionToLogin();
            }
            else
            {
                Debug.LogWarning($"[ClientAuthFlow] [Client] 회원가입 실패: {msg.Message}");
            }

            NetworkClient.Disconnect();
        }

        private void OnEnterGameMessage(EnterGameMessage msg)
        {
            Debug.Log($"[ClientAuthFlow] [Client] EnterGameMessage | MatchId: {msg.MatchId} | Difficulty: {msg.Difficulty}");
            _uiBridge?.TransitionToInGame();
        }

        private void ClearPendingData()
        {
            _pendingMode     = PendingMode.None;
            _pendingUserId   = null;
            _pendingPassword = null;
            _pendingUserName = null;
        }

        #endregion
    }
}
