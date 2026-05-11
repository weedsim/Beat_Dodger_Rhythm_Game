namespace BeatDodger.Network
{
    public interface IClientAuthFlow
    {
        void PrepareLogin(string userId, string password);
        void PrepareRegister(string userId, string password, string userName);
        void OnConnected();
    }
}
