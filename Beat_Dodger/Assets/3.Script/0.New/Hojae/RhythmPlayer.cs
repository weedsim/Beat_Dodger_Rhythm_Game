using UnityEngine;
using Mirror;

public class RhythmPlayer : NetworkBehaviour
{
    [SyncVar] public int myLaneIndex = -1; // 내가 배정받은 레인 번호 (초기값 -1)

    // 서버에서만 관리하는 '번호표 기계' (다음 접속자는 몇 번 레인?)
    private static int nextLaneToAssign = 0;

    // 1. 플레이어가 방에 접속하면 서버가 번호표(레인)를 쥐어줍니다!
    public override void OnStartServer()
    {
        myLaneIndex = nextLaneToAssign;
        nextLaneToAssign++;

        // 만약 4명이 다 찼는데 또 들어오면 다시 0번부터 (예외 처리)
        if (nextLaneToAssign > 3) nextLaneToAssign = 0;
    }

    private void Update()
    {
        // 내 컴퓨터의 내 아바타가 아니면 조작 불가!
        if (!isLocalPlayer) return;

        // 2. 내가 스페이스바를 쾅 쳤다!!
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 서버 대장님한테 "저 스페이스바 쳤어요!" 라고 귓속말(Command)을 보냅니다.
            CmdPressMyLane(myLaneIndex);
        }
    }

    //  [Command]: 클라이언트 -> 서버로 쏘는 명령
    [Command]
    private void CmdPressMyLane(int lane)
    {
        // 서버가 명령을 받으면, "모든 사람 화면에 n번 레인 이펙트 터트려라!" 라고 방송합니다.
        RpcPressMyLane(lane);
    }

    //  [ClientRpc]: 서버 -> 모든 클라이언트로 쏘는 방송
    [ClientRpc]
    private void RpcPressMyLane(int lane)
    {
        // 3. 드디어 대장님(Manager)의 통로를 열어 내 레인을 타격합니다!!
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.TriggerLaneInput(lane);
        }
    }
}