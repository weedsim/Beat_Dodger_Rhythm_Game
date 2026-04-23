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
    /// 서버 사이드 로그인 요청 처리 구현체.
    /// DB 조회 및 세션 상태 전이를 담당한다.
    /// </summary>
    public class LoginHandler : ILoginHandler
    {
        #region Variables

        private readonly DBService _dbService;
        private readonly SessionCoordinator _sessionCoordinator;

        #endregion

        #region Constructor

        /// <summary>
        /// LoginHandler 생성자.
        /// </summary>
        /// <param name="dbService">DB 서비스 인스턴스</param>
        /// <param name="sessionCoordinator">세션 코디네이터 인스턴스</param>
        public LoginHandler(DBService dbService, SessionCoordinator sessionCoordinator)
        {
            _dbService = dbService;
            _sessionCoordinator = sessionCoordinator;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 클라이언트로부터 수신한 로그인 요청을 비동기적으로 처리한다.
        /// async void 대신 내부에서 Task를 발행하여 예외가 소실되지 않도록 한다.
        /// </summary>
        /// <param name="conn">로그인 요청한 클라이언트의 네트워크 연결</param>
        /// <param name="msg">로그인 요청 메시지</param>
        public void HandleLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            // C4: async void 제거 — Task를 발행하고 예외는 내부에서 처리
            _ = HandleLoginRequestAsync(conn, msg);
        }

        #endregion

        #region Private Methods

        private async Task HandleLoginRequestAsync(NetworkConnectionToClient conn, LoginRequestMessage msg)
        {
            try
            {
                Debug.Log($"[LoginHandler] [Server] 로그인 요청 수신 | UserId: {msg.UserId}");

                UserData userData = await _dbService.GetUserOrCreateAsync(msg.UserId);

                if (userData._UserId != null)
                {
                    // 임시 userId 를 실제 DB 값으로 교체하고 UserName 도 세션에 저장한다.
                    _sessionCoordinator.UpdatePlayerUserInfo(conn, userData._UserId, userData._UserName);
                    _sessionCoordinator.UpdatePlayerState(conn, SessionState.Lobby);

                    LoginResponseMessage response = new LoginResponseMessage
                    {
                        Success = true,
                        UserId = userData._UserId,
                        UserName = userData._UserName,
                        Level = userData._Level,
                        Currency = userData._Currency
                    };

                    conn.Send(response);
                    Debug.Log($"[LoginHandler] [Server] 로그인 성공 | UID: {userData._UserId} | UserName: {userData._UserName}");
                }
                else
                {
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

        #endregion
    }
}
