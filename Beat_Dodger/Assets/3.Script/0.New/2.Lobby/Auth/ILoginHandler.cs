using Mirror;
using BeatDodger.Network;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버 사이드 로그인 요청 처리 추상화 인터페이스.
    /// </summary>
    public interface ILoginHandler
    {
        void HandleLoginRequest(NetworkConnectionToClient conn, LoginRequestMessage msg);
        void HandleRegisterRequest(NetworkConnectionToClient conn, RegisterRequestMessage msg);
    }
}
