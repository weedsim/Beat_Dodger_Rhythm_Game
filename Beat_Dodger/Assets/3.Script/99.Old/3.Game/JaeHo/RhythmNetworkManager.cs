using System.Collections;
using UnityEngine;
using Mirror;

public class RhythmNetworkManager : NetworkManager
{
    public static new RhythmNetworkManager Instance;
    public override void Start()
    {
        base.Start();

#if UNITY_EDITOR
        // 에디터에서는 자동으로 Host 시작
        Debug.Log("에디터: 자동 Host 시작!");
        StartHost();
#endif
    }
    public override void Awake()
    {
        base.Awake();
        Instance = this;
    }
    public bool isLoggedIn = false;
    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log("서버 연결 완료!");
        if (isLoggedIn)
        {
            LoginUIManager.Instance.ShowLobby();
        }
    }

    // 서버에서 모든 클라이언트에게 시작 신호 보내기
    public void ServerStartCountdown()
    {
        if (!NetworkServer.active) return;
        Debug.Log("[서버] 카운트다운 시작!");

        // 연결된 모든 클라이언트에게 메시지 보내기
        foreach (var conn in NetworkServer.connections.Values)
        {
            conn.Send(new StartGameMessage());
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // 서버에서 StartGame 메시지 오면 처리
        NetworkClient.RegisterHandler<StartGameMessage>(OnStartGameReceived);
    }

    private void OnStartGameReceived(StartGameMessage msg)
    {
        Debug.Log("[클라이언트] 게임 시작!");
        LoginUIManager.Instance.StartGame();
        StartCoroutine(WaitAndStartMusic(msg.exactStartTime));
    }

    private IEnumerator WaitAndStartMusic(double exactStartTime)
    {
        while (NetworkTime.time < exactStartTime)
            yield return null;
        Debug.Log("노트 내려오기 시작!");
    }
}

// 메시지 구조체 (파일 맨 아래에 추가)
public struct StartGameMessage : NetworkMessage
{
    public double exactStartTime;
}