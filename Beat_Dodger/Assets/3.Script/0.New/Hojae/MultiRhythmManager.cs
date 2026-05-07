using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class MultiRhythmManager : NetworkBehaviour
{
    public static MultiRhythmManager Instance { get; private set; }

    [Header("Rhythm Settings")]
    [SerializeField] private float bpm = 120f;
    [SyncVar] private double exactStartTime; // [중요] 모든 클라이언트가 공유하는 시작 시간

    [Header("Sync Settings")]
    private HashSet<int> hitNoteIds = new HashSet<int>(); // 서버만 관리하는 "이미 처리된 노트" 목록

    //  [띠또가 추가한 부분 1: 번호표 기계와 살아있는 노트 명부!] 
    private int _globalNoteId = 0; // 0번부터 시작하는 노트 번호표
    public List<NoteEnemy> activeNotes = new List<NoteEnemy>(); // 현재 화면에 내려오고 있는 노트들
    //  ------------------------------------------------------ 

    private bool _isGameStarted = false;
    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        _audioSource = GetComponent<AudioSource>();
    }

    // 1. [서버] 게임 시작 명령 (방장이 버튼 누를 때 호출)
    [Server]
    public void ServerStartGame()
    {
        // 3초 뒤에 모든 유저가 동시에 시작하도록 설정
        exactStartTime = NetworkTime.time + 3.0f;
        _isGameStarted = true;
        RpcStartMusic(exactStartTime);
    }

    // 2. [모든 클라이언트] 음악 시작 방송
    [ClientRpc]
    private void RpcStartMusic(double startTime)
    {
        exactStartTime = startTime;
        _isGameStarted = true;
    }

    private void Update()
    {
        if (!_isGameStarted) return;

        // [입력 조작] 스페이스바를 누르면 서버에 보고!
        if (isLocalPlayer && Input.GetKeyDown(KeyCode.Space))
        {
            // 가장 가까운 노트 ID를 찾아 서버에 보냅니다.
            int targetNoteId = GetNearestNoteId();
            if (targetNoteId != -1)
            {
                CmdRequestHitNote(targetNoteId);
            }
        }
    }

    //  [띠또가 추가한 부분 2: 노트를 스폰할 때 번호표 달아주기!]
    // 기존에 노트를 생성(Instantiate)하거나 풀(Pool)에서 꺼내오는 함수가 있다면,
    // 그 안에서 노트를 꺼낸 직후에 아래 세 줄을 꼭 넣어주시옵소서!
    public void RegisterNewNote(NoteEnemy newNote)
    {
        newNote.myNoteId = _globalNoteId; // "너는 O번 노트다!" 명찰 달아주기
        _globalNoteId++;                  // 다음 노트를 위해 번호표 1 증가

        activeNotes.Add(newNote);         // 살아있는 노트 명부에 등록!
    }

    // 3. [클라이언트 -> 서버] "저 이 노트 맞췄어요!" 보고
    [Command]
    private void CmdRequestHitNote(int noteId, NetworkConnectionToClient sender = null)
    {
        // [서버의 냉정한 판정] 이미 누가 쳤나?
        if (hitNoteIds.Contains(noteId)) return;

        hitNoteIds.Add(noteId); // 명단에 추가 (선착순 고정)

        // 판정 계산 (서버에서 시간 대조)
        Judgment result = CalculateJudgment(noteId);

        // 모든 클라이언트에게 "얘가 이거 맞췄으니 터뜨려!" 라고 방송
        RpcNotifyHit(noteId, sender.connectionId, result);
    }

    // 4. [서버 -> 모든 클라이언트] 판정 결과 공유 및 연출
    [ClientRpc]
    private void RpcNotifyHit(int noteId, int playerConnId, Judgment result)
    {
        RemoveNoteFromScreen(noteId);
        Debug.Log($"플레이어 [{playerConnId}]님이 [{result}] 판정으로 노트를 제거했사옵니다!");
    }

    // 👇 [띠또가 추가한 부분 3: 제일 밑에 있는 노트 찾기 진짜 로직!] 👇
    private int GetNearestNoteId()
    {
        if (activeNotes.Count > 0)
        {
            // 리스트의 0번째가 항상 판정선에 제일 가까운 놈이옵니다!
            return activeNotes[0].myNoteId;
        }
        return -1; // 칠 노트가 없으면 -1 반환
    }

    private Judgment CalculateJudgment(int noteId) { return Judgment.Perfect; }

    private void RemoveNoteFromScreen(int noteId)
    {
        // 노트 지우기 로직 (명부에서도 빼줘야 하옵니다!)
        NoteEnemy targetNote = activeNotes.Find(n => n.myNoteId == noteId);
        if (targetNote != null)
        {
            activeNotes.Remove(targetNote);
            Destroy(targetNote.gameObject); // 풀을 쓴다면 Release!
        }
    }
}