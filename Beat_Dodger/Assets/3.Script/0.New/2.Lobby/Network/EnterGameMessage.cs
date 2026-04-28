using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버가 매치 시작 시 파티 참가자 4명 전원에게 전송하는 메시지.
    /// 수신한 클라이언트는 UIManager.TransitionToInGame()을 호출하여 인게임 화면으로 전환한다.
    /// </summary>
    public struct EnterGameMessage : NetworkMessage
    {
        /// <summary>생성된 매치의 고유 ID</summary>
        public int MatchId;

        /// <summary>방 생성 시 설정된 난이도</summary>
        public int Difficulty;

        /// <summary>방장이 선택한 곡 고유 ID</summary>
        public int SongId;

        /// <summary>방장이 선택한 곡 이름</summary>
        public string SongName;
    }
}
