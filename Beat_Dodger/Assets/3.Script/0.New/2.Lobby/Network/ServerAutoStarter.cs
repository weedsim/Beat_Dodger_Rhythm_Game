using Mirror;
using UnityEngine;

namespace BeatDodger.Network
{
    /// <summary>
    /// 헤드리스 서버 빌드에서 NetworkManager.StartServer()를 명시적으로 호출한다.
    /// Mirror 버전에 따라 자동 시작이 안 되는 경우의 안전망.
    /// 씬의 NetworkManager GameObject와 같은 GameObject에 붙이거나 별도 루트 GameObject에 붙인다.
    /// </summary>
    public class ServerAutoStarter : MonoBehaviour
    {
        private void Start()
        {
            if (!Application.isBatchMode) return;

            if (NetworkServer.active)
            {
                Debug.Log("[ServerAutoStarter] 서버가 이미 실행 중입니다.");
                return;
            }

            if (NetworkManager.singleton == null)
            {
                Debug.LogError("[ServerAutoStarter] NetworkManager.singleton이 null입니다. 씬에 NetworkManager가 있는지 확인하세요.");
                return;
            }

            Debug.Log("[ServerAutoStarter] 헤드리스 모드 감지 — StartServer() 호출");
            NetworkManager.singleton.StartServer();
        }
    }
}
