using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 클라이언트가 서버에 로그인을 요청할 때 보내는 메시지
    /// </summary>
    public struct LoginRequestMessage : NetworkMessage
    {
        public string UserId;
        public string Password;
    }

    /// <summary>
    /// 서버가 클라이언트에게 로그인 결과와 유저 데이터를 응답하는 메시지
    /// </summary>
    public struct LoginResponseMessage : NetworkMessage
    {
        public bool Success;
        public string UserId;
        public string UserName;
        public int Level;
        public int Currency;
    }

    /// <summary>
    /// 파티장이 게임 시작을 요청할 때 보내는 메시지
    /// </summary>
    public struct StartMatchRequestMessage : NetworkMessage
    {
        public string MatchName;
        public System.Collections.Generic.List<int> ParticipantNetIds;
    }
}