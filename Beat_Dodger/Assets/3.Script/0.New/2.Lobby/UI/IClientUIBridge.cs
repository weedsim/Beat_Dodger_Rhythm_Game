namespace BeatDodger.UI
{
    public interface IClientUIBridge
    {
        void TransitionToLogin();
        void TransitionToLobby();
        void TransitionToInGame();
    }
}
