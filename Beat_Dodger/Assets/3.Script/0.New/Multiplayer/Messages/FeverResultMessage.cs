using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 피버 연타 종료 후 결과를 파티 4명에게 전송.
    /// 클라이언트는 이 메시지를 받아 보스 애니메이션·이펙트를 재생한다.
    /// </summary>
    public struct FeverResultMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID</summary>
        public int MatchId;

        /// <summary>연타 성공 여부</summary>
        public bool Success;

        /// <summary>레이저 결투 성공으로 게임이 클리어됐는지 여부</summary>
        public bool GameCleared;

        /// <summary>이번 피버 처리 후의 누적 성공 횟수 (이펙트 선택에 사용)</summary>
        public int FeverSuccessCount;
    }
}
