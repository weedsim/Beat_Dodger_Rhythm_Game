using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버가 노트를 스폰할 때 파티 4명에게 전송하는 메시지.
    /// 클라이언트는 수신 즉시 hitTime을 기준으로 NoteEnemy를 로컬 스폰한다.
    /// </summary>
    public struct NoteSpawnMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID (수신 클라이언트 검증용)</summary>
        public int MatchId;

        /// <summary>이 노트의 고유 ID. 서버 글로벌 카운터로 발급.</summary>
        public int NoteId;

        /// <summary>시작 레인 인덱스 (0~3)</summary>
        public int Lane;

        /// <summary>차지하는 레인 수 (1=단일, 2~4=코드)</summary>
        public int Span;

        /// <summary>노트 타입</summary>
        public NoteType NoteType;

        /// <summary>
        /// 판정선에 도달해야 하는 절대 DSP 시간.
        /// 클라이언트는 이 값으로 NoteEnemy.Initialize()를 호출한다.
        /// </summary>
        public double HitTime;
    }
}
