using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 피버 연타 진행 중 서버가 파티 4명에게 주기적으로 전송.
    /// Progress 값으로 레이저 위치 및 게이지 시각화에 사용.
    /// </summary>
    public struct FeverUpdateMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID</summary>
        public int MatchId;

        /// <summary>연타 진행률 (0 ~ 1). 레이저 결투에서는 클래시 위치 비율.</summary>
        public float Progress;

        /// <summary>남은 시간 (초)</summary>
        public float TimeRemaining;
    }
}
