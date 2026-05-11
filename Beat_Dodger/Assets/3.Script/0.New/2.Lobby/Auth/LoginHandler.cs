using System;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using BeatDodger.Core;
using BeatDodger.Services;
using BeatDodger.Managers;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버 사이드 로그인/회원가입 요청 처리 구현체.
    /// DB 조회 및 세션 상태 전이를 담당한다.
    /// </summary>
    public class LoginHandler : ILoginHandler
    {
        #region Variables

        private readonly IDBService _dbService;
        private readonly SessionCoordinator _sessionCoordinator;

        #endregion

        #region Constructor

        public LoginHandler(IDBService dbService, SessionCoordinator sessionCoordinator)
        {
            _dbService = dbService;
            _sessionCoordinator = sessionCoordinator;
        }

        #endregion

        #region Public Methods

        public void HandleLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            _ = HandleLoginRequestAsync(conn, msg);
        }

        public void HandleRegisterRequest(NetworkConnectionToClient conn, RegisterRequestMessage msg)
        {
            _ = HandleRegisterRequestAsync(conn, msg);
        }

        #endregion

        #region Private Methods

        private async Task HandleLoginRequestAsync(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            try
            {
                Debug.Log($"[LoginHandler] [Server] 로그인 요청 수신 | UserId: {msg.UserId}");

                UserData userData = await _dbService.AuthenticateUserAsync(msg.UserId, msg.Password);

                if (!string.IsNullOrEmpty(userData._UserId))
                {
                    _sessionCoordinator.UpdatePlayerUserInfo(conn, userData._UserId, userData._UserName);
                    _sessionCoordinator.UpdatePlayerState(conn, SessionState.Lobby);

                    conn.Send(new LoginResponseMessage
                    {
                        Success   = true,
                        UserId    = userData._UserId,
                        UserName  = userData._UserName,
                        Volume    = userData._Volume,
                        Sync      = userData._Sync,
                        InputKey  = userData._InputKey,
                        FrameRate = userData._FrameRate
                    });

                    Debug.Log($"[LoginHandler] [Server] 로그인 성공 | UID: {userData._UserId} | UserName: {userData._UserName}");
                }
                else
                {
                    Debug.LogWarning($"[LoginHandler] [Server] 로그인 실패 | UserId: {msg.UserId}");
                    conn.Send(new LoginResponseMessage { Success = false });
                    conn.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoginHandler] [Server] 로그인 처리 중 예외 발생: {ex}");
                conn.Send(new LoginResponseMessage { Success = false });
                conn.Disconnect();
            }
        }

        private async Task HandleRegisterRequestAsync(NetworkConnectionToClient conn, RegisterRequestMessage msg)
        {
            try
            {
                Debug.Log($"[LoginHandler] [Server] 회원가입 요청 수신 | UserId: {msg.UserId}");

                bool success = await _dbService.RegisterUserAsync(msg.UserId, msg.Password, msg.TempNickName);

                string message = success
                    ? "회원가입이 완료되었습니다."
                    : "이미 존재하는 아이디입니다.";

                conn.Send(new RegisterResponseMessage { Success = success, Message = message });

                Debug.Log($"[LoginHandler] [Server] 회원가입 처리 완료 | UserId: {msg.UserId} | Success: {success}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoginHandler] [Server] 회원가입 처리 중 예외 발생: {ex}");
                conn.Send(new RegisterResponseMessage { Success = false, Message = "서버 오류가 발생했습니다." });
            }
        }

        #endregion
    }
}
