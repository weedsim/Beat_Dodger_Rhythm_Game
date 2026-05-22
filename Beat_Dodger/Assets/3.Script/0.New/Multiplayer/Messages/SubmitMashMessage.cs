using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 피버 연타 중 클라이언트가 레인 키를 누를 때마다 서버에 전송.
    /// NetworkClient.Send()로 전송, 서버는 NetworkServer.RegisterHandler로 수신.
    /// </summary>
    public struct SubmitMashMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID (서버가 해당 매치 상태에 연타 수 반영)</summary>
        public int MatchId;
    }
}
