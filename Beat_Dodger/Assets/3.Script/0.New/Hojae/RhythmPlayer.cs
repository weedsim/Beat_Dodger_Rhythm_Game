using UnityEngine;
using Mirror;

public class RhythmPlayer : NetworkBehaviour
{
    [SyncVar] public int myLaneIndex = -1;
    private static int nextLaneToAssign = 0;

    public override void OnStartServer()
    {
        myLaneIndex = nextLaneToAssign;
        nextLaneToAssign++;
        if (nextLaneToAssign > 3) nextLaneToAssign = 0;
    }

    public override void OnStartLocalPlayer()
    {
        // 내 레인 번호를 RhythmManager에 알려주기
        if (NewRhythmManager.Instance != null)
            NewRhythmManager.Instance.myLaneIndex = myLaneIndex;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // 스페이스바 - 노트 타격
        if (Input.GetKeyDown(KeyCode.Space))
            CmdPressMyLane(myLaneIndex);

        // 엔터 - 게임 시작
        if (Input.GetKeyDown(KeyCode.Return))
            CmdRequestGameStart();
    }

    [Command]
    private void CmdPressMyLane(int lane)
    {
        RpcPressMyLane(lane);
    }

    [ClientRpc]
    private void RpcPressMyLane(int lane)
    {
        if (NewRhythmManager.Instance != null)
            NewRhythmManager.Instance.TriggerLaneInput(lane);
    }

    [Command]
    private void CmdRequestGameStart()
    {
        if (NewRhythmManager.Instance != null &&
            !NewRhythmManager.Instance.isGameStart)
        {
            double startTime = AudioSettings.dspTime + 3.0;
            RpcStartGame(startTime);
        }
    }

    [ClientRpc]
    private void RpcStartGame(double startTime)
    {
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.isGameStart = true;
            NewRhythmManager.Instance.exactStartTime = startTime;
        }
    }
    [Command]
    public void CmdHitNote(int laneIndex, int noteId)
    {
        RpcNotifyHitNote(noteId);
    }

    [ClientRpc]
    private void RpcNotifyHitNote(int noteId)
    {
        if (NewRhythmManager.Instance == null) return;
        NoteEnemy targetNote = NewRhythmManager.Instance.activeNotes
            .Find(n => n.myNoteId == noteId);
        if (targetNote != null)
        {
            targetNote.ReleaseToPool();
            NewRhythmManager.Instance.activeNotes.Remove(targetNote);
        }
    }
}