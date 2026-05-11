using Mirror;

namespace BeatDodger.Network
{
    public interface IPlayerSpawner
    {
        void SpawnPlayer(NetworkConnectionToClient conn);
    }
}
