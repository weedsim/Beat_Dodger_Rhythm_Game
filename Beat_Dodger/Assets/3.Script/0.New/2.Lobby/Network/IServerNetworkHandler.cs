using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버 사이드 네트워크 이벤트 처리 인터페이스.
    /// 서버 전용 소비자는 이 인터페이스에만 의존한다.
    /// </summary>
    public interface IServerNetworkHandler
    {
        void OnServerConnect(NetworkConnectionToClient conn);
        void OnServerAddPlayer(NetworkConnectionToClient conn);
        void OnServerDisconnect(NetworkConnectionToClient conn);
        void OnStartServer();
    }
}
