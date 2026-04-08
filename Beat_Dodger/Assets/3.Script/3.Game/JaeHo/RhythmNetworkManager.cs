using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class RhythmNetworkManager : NetworkBehaviour
{
    public static RhythmNetworkManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    // =========================================================
    // 1. 동시 시작 (카운트다운) 기능
    // =========================================================
    
    // 로비에서 전원 레디 시 호출되는 함수 (서버 전용)
    // =========================================================
    [Server]
    public void ServerStartCountdown()
    {
        // 현재 시간 + 3초 뒤를 시작 시간으로 계산
        double exactStartTime = NetworkTime.time + 3.0d;
        RpcStartGame(exactStartTime);
    }

    [Command(requiresAuthority = false)] // 클라이언트 -> 서버로 시작 요청
    public void CmdRequestStartGame()
    {
        Debug.Log("[서버] 누군가 게임 시작을 요청했습니다!");
        // 서버의 현재 시간에서 딱 3초 뒤의 시간을 계산
        double exactStartTime = NetworkTime.time + 3.0d;

        // 모든 접속자에게 "3초 뒤에 시작해라!" 방송
        RpcStartGame(exactStartTime);
    }

    // 서버가 모든 클라이언트에게 뿌리는 방송 (UI 끄기 + 코루틴)
    // =========================================================
    [ClientRpc]
    public void RpcStartGame(double exactStartTime)
    {
        // 1. 게임 화면으로 넘어가기 위해 로비 UI를 끕니다!
        LoginUIManager.Instance.StartGame();

        Debug.Log($"[클라이언트] 카운트다운 시작! 남은 시간: {exactStartTime - NetworkTime.time}초");
        StartCoroutine(WaitAndStartMusic(exactStartTime));
    }

    private IEnumerator WaitAndStartMusic(double exactStartTime)
    {
        // 서버가 정해준 '그 시간'이 될 때까지 무한 대기!
        while (NetworkTime.time < exactStartTime)
        {
            yield return null;
        }

        //  여기서부터 진짜 시작! (주인님의 로컬 게임 시작 코드 연결)
        Debug.Log(" 노트 내려오기 시작");
        //FindObjectOfType<BeatDodger.Game.RhythmManager>().StartGameLogic();
    }

    // =========================================================
    // 2. 죽음 및 관전 모드 전환 기능
    // =========================================================

    // 체력이 0이 되면 이 함수를 호출
    // ex) RhythmNetworkManager.Instance.CmdReportDeath(gameObject);
    [Command(requiresAuthority = false)]
    public void CmdReportDeath(GameObject deadPlayer)
    {
        Debug.Log($"[서버] {deadPlayer.name} 유저 사망 접수. 관전 모드 방송 시작!");
        RpcOnPlayerDied(deadPlayer);
    }

    [ClientRpc]
    public void RpcOnPlayerDied(GameObject deadPlayer)
    {
        // 만약 죽은 사람이 '나(로컬 플레이어)'라면?
        if (deadPlayer.GetComponent<NetworkIdentity>().isLocalPlayer)
        {
            Debug.Log(" 죽음! 관전 모드로 전환합니다!");

            // 1. 내 리듬 게임 UI 끄기
            // myRhythmUI.SetActive(false);

            // 2. 다른 살아있는 팀원을 찾아서 카메라 붙이기
            SpectateAlivePlayer();
        }
        else
        {
            // 다른 팀원이 죽었을 때 내 화면에 띄울 알림
            Debug.Log($"알림: 팀원 전사! ㅠㅠ");
        }
    }

    private void SpectateAlivePlayer()
    {
        // 씬에 있는 Player 태그를 가진 사람들을 싹 찾아서,
        // 내가 아닌 다른 사람(살아있는 사람)에게 카메라를 이동시키는 로직!
        Debug.Log(" 살아있는 팀원의 화면을 훔쳐봅니다...");
        // ex) Camera.main.transform.SetParent(alivePlayer.transform);
    }
}
