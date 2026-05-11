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
            RpcStartGame(); // 시간 안 보냄
        }
    }


    [ClientRpc]
    private void RpcStartGame()
    {
        if (NewRhythmManager.Instance != null)
        {
            NewRhythmManager.Instance.isGameStart = true;
            // 각자 받는 순간 기준으로 5초 뒤
            NewRhythmManager.Instance.exactStartTime = AudioSettings.dspTime + 5.0;
            NewRhythmManager.Instance.currentNoteIndex = 0;

            Debug.Log($"exactStartTime: {NewRhythmManager.Instance.exactStartTime}, dspTime: {AudioSettings.dspTime}");
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