using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버가 매치 시작 시 파티 4명에게 전송하는 메시지.
    /// SessionCoordinator.SendToMatch()로 전송되므로 파티 격리가 보장된다.
    /// </summary>
    public struct GameStartMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID</summary>
        public int MatchId;

        /// <summary>선택된 곡 고유 ID</summary>
        public int SongId;

        /// <summary>선택된 곡 이름</summary>
        public string SongName;

        /// <summary>난이도</summary>
        public int Difficulty;

        /// <summary>
        /// 모든 클라이언트가 동시에 음악을 시작할 기준 시간.
        /// AudioSettings.dspTime 기반 절대 시간(서버 NetworkTime 보정 포함).
        /// </summary>
        public double ServerStartDspTime;

        /// <summary>BPM (노트 이동 계산에 사용)</summary>
        public float Bpm;

        /// <summary>판정선까지 걸리는 박자 수 (노트 이동 계산에 사용)</summary>
        public int BeatsToArrive;

        /// <summary>이 클라이언트의 슬롯 인덱스 (0~3)</summary>
        public int SlotIndex;
    }
}
