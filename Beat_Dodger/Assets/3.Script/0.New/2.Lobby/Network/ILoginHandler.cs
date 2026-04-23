using Mirror;
using BeatDodger.Network;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버 사이드 로그인 요청 처리 추상화 인터페이스.
    /// </summary>
    public interface ILoginHandler
    {
        /// <summary>
        /// 클라이언트로부터 수신한 로그인 요청을 비동기적으로 처리한다.
        /// </summary>
        /// <param name="conn">로그인 요청한 클라이언트의 네트워크 연결</param>
        /// <param name="msg">로그인 요청 메시지</param>
        void HandleLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg);
    }
}
