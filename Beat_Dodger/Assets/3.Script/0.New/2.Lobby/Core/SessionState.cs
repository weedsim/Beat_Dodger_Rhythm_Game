namespace BeatDodger.Core
{
    /// <summary>
    /// 플레이어의 현재 네트워크 세션 상태
    /// </summary>
    public enum SessionState
    {
        Authenticating, // 로그인/인증 진행 중 (DB 검증 단계)
        Lobby,      // 로비 대기 중
        Loading,    // 인게임 진입 로딩 중
        InGame,     // 게임 플레이 중
        Result      // 결과 창 확인 중
    }

    /// <summary>
    /// DB에서 로드한 사용자 기본 정보
    /// </summary>
    [System.Serializable]
    public struct UserData
    {
        public string _UserId;
        public string _UserName;
        public int _Level;
        public int _Experience;
        public int _Currency;
    }
}