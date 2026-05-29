using Mirror;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버가 노트 히트 처리 후 파티 4명에게 브로드캐스트하는 메시지.
    /// 개인 판정 결과(TargetRpc)와 달리, 이 메시지는 다른 플레이어의 결과를 보여주기 위해 사용된다.
    /// [수정] FeverGauge 필드 추가 — 파티 공유 피버 게이지를 모든 파티원에게 동기화하기 위함.
    ///        개인 TargetRpc에서는 게이지 갱신을 제거하고 이 브로드캐스트로 통일.
    /// </summary>
    public struct NoteHitResultMessage : NetworkMessage
    {
        /// <summary>매치 고유 ID</summary>
        public int MatchId;

        /// <summary>처리된 노트 고유 ID</summary>
        public int NoteId;

        /// <summary>히트한 플레이어의 NetId</summary>
        public uint HitterNetId;

        /// <summary>판정 결과</summary>
        public Judgment Judgment;

        /// <summary>히트한 플레이어의 현재 콤보</summary>
        public int Combo;

        /// <summary>파티 공유 피버 게이지 (0~100). 모든 파티원이 UI 갱신에 사용.</summary>
        public float FeverGauge;
    }
}
