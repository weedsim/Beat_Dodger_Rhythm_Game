using UnityEngine;
using BeatDodger.Database;
using BeatDodger.Managers;
using BeatDodger.Services;

namespace BeatDodger.Network
{
    /// <summary>
    /// 서버 컴포넌트 생성 팩토리. 서브클래스로 오버라이드하여 구현체를 교체한다.
    /// (예: 테스트용 MockServerComponentFactory, 스테이징용 StagingServerComponentFactory)
    /// </summary>
    [CreateAssetMenu(fileName = "ServerComponentFactory", menuName = "BeatDodger/Server Component Factory")]
    public class ServerComponentFactory : ScriptableObject
    {
        public virtual IDBService CreateDBService()
        {
            return new DBService(new DBRepository());
        }

        public virtual ILoginHandler CreateLoginHandler(IDBService dbService, SessionCoordinator coordinator)
        {
            return new LoginHandler(dbService, coordinator);
        }

        public virtual IPlayerSpawner CreatePlayerSpawner(GameObject playerPrefab, SessionCoordinator coordinator)
        {
            return new ServerPlayerSpawner(playerPrefab, coordinator);
        }
    }
}
