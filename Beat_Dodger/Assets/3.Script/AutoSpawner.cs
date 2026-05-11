using Mirror;
using UnityEngine;

public class AutoSpawner : NetworkBehaviour
{
    public GameObject managerPrefab; // 여기에 아까 만든 리듬매니저 프리팹을 넣으세요.

    public override void OnStartServer()
    {
        // 서버가 시작되면 매니저를 자동으로 소환하고 네트워크 도장을 찍어줍니다.
        GameObject mg = Instantiate(managerPrefab);
        NetworkServer.Spawn(mg);
        Debug.Log("서버: 리듬 매니저 소환 완료! 이제 netId가 0이 아닐 겁니다.");
    }
}