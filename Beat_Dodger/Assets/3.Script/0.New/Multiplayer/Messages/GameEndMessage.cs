using Mirror;

namespace BeatDodger.Network
{
    public enum GameEndReason
    {
        /// <summary>모든 노트 처리 완료 — 정상 클리어</summary>
        Cleared = 0,

        /// <summary>플레이어 전원 접속 종료</summary>
        AllDisconnected = 1,
    }

    /// <summary>
    /// 서버가 게임 종료 시 파티 4명에게 전송하는 메시지.
    /// </summary>
    public struct GameEndMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID</summary>
        public int MatchId;

        /// <summary>종료 사유</summary>
        public GameEndReason Reason;
    }
}
