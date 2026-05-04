namespace BeatDodger.Lobby
{
    /// <summary>
    /// 파티 시스템에서 사용하는 상수 정의 클래스.
    /// </summary>
    public static class PartyConstants
    {
        /// <summary>파티 최대 인원 수</summary>
        public const int MAX_PARTY_MEMBERS = 4;

        /// <summary>로비에서 허용하는 기본 최대 파티 수</summary>
        public const int DEFAULT_MAX_PARTIES = 20;

        /// <summary>방 이름 최대 길이 (서버 입력 검증용)</summary>
        public const int MAX_ROOM_NAME_LENGTH = 32;

        /// <summary>방 비밀번호 최대 길이 (서버 입력 검증용)</summary>
        public const int MAX_PASSWORD_LENGTH = 32;

        /// <summary>곡 이름 최대 길이 (서버 입력 검증용)</summary>
        public const int MAX_SONG_NAME_LENGTH = 64;

        /// <summary>곡 해시태그 분위기 문자열 최대 길이 (서버 입력 검증용)</summary>
        public const int MAX_SONG_TAGS_LENGTH = 128;
    }
}
