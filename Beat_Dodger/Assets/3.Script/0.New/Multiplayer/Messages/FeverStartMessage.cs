using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버가 파티 공유 피버 게이지가 100% 도달 시 파티 4명에게 전송.
    /// SessionCoordinator.SendToMatch()로 파티 격리 보장.
    /// </summary>
    public struct FeverStartMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID</summary>
        public int MatchId;

        /// <summary>피버 단계 (1·2 = 연타, 3 이상 = 레이저 결투)</summary>
        public int FeverStage;

        /// <summary>연타 제한 시간 (초)</summary>
        public float Duration;

        /// <summary>성공 기준 연타 수</summary>
        public float TargetMashCount;

        /// <summary>연타 시작값 (레이저 결투는 목표의 0.5, 일반은 0)</summary>
        public float InitialMashFloat;
    }
}
