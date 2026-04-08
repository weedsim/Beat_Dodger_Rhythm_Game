using UnityEngine;
using Mirror;

public class LobbyPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnReadyChanged))]
    public bool isReady = false;

    // 유저 번호 (1P는 0, 2P는 1)
    [SyncVar(hook = nameof(OnPlayerIndexChanged))]
    public int playerIndex = -1;

    // 서버가 클라이언트를 처음 받을 때 순서대로 번호를 부여함!
    public override void OnStartServer()
    {
        // 현재 로비에 들어온 사람 수를 세서 내 번호로 지정
        playerIndex = FindObjectsOfType<LobbyPlayer>().Length - 1;
    }

    // 클라이언트가 켜지면 무조건 화면 갱신 한 번 때려주기
    public override void OnStartClient()
    {
        UpdateLobbyUI();
    }

    // SyncVar 값이 변할 때마다 자동으로 화면 갱신
    void OnPlayerIndexChanged(int oldVal, int newVal) { UpdateLobbyUI(); }
    void OnReadyChanged(bool oldVal, bool newVal) { UpdateLobbyUI(); }

    void Update()
    {
        // 내 캐릭터일 때 'R' 키를 누르면 레디!
        if (isLocalPlayer && Input.GetKeyDown(KeyCode.R))
        {
            CmdToggleReady();
        }
    }

    [Command]
    public void CmdToggleReady()
    {
        isReady = !isReady;
        CheckAllReady();
    }

    // 핵심: 모든 클라이언트가 자기 화면의 UI를 새로고침 하는 함수
    private void UpdateLobbyUI()
    {
        if (LoginUIManager.Instance == null) return;

        if (playerIndex >= 0)
        {
            LoginUIManager.Instance.UpdatePlayerStatus(playerIndex, isReady);
        }
    }

    [Server]
    private void CheckAllReady()
    {
        LobbyPlayer[] players = FindObjectsOfType<LobbyPlayer>();
        bool allReady = true;
        foreach (var p in players)
        {
            if (!p.isReady) { allReady = false; break; }
        }

        if (allReady)
        {
            RpcTransitionToGameUI();
            Debug.Log(" [서버] 전원 레디 완료! 카운트다운 시작!");
            if (RhythmNetworkManager.Instance != null)
                RhythmNetworkManager.Instance.ServerStartCountdown();
        }
    }
    [ClientRpc]
    private void RpcTransitionToGameUI()
    {
        if (LoginUIManager.Instance != null)
        {
            LoginUIManager.Instance.StartGame(); // 각자의 컴퓨터에서 로비창 끄고 게임창 켜기!
        }
    }
}