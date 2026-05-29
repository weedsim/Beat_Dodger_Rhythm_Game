namespace BeatDodger.Core
{
    /// <summary>
    /// 파티룸에서 플레이어가 선택할 수 있는 악기 종류를 정의한다.
    /// None은 선택하지 않은 상태를 나타낸다.
    /// </summary>
    public enum InstrumentType
    {
        /// <summary>선택 없음 (기본값)</summary>
        None,

        /// <summary>드럼</summary>
        Drum,

        /// <summary>기타</summary>
        Guitar,

        /// <summary>베이스</summary>
        Bass,

        /// <summary>키보드</summary>
        Keyboard
    }
}
