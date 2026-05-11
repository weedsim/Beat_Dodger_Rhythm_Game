namespace BeatDodger.Network
{
    /// <summary>
    /// 클라이언트가 로그인을 시작하기 위해 필요한 최소 인터페이스.
    /// LoginUIController 등 UI 레이어는 이 인터페이스에만 의존한다.
    /// </summary>
    public interface ILoginEntryPoint
    {
        void AttemptLogin(string userId, string password);
        void AttemptRegister(string userId, string password, string userName);
    }
}
