using Mirror;
using UnityEngine;

public class MyNetworkManager : NetworkManager
{
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("서버 연결 완료!");
        LoginUIManager.Instance.ShowLobby();
    }
}