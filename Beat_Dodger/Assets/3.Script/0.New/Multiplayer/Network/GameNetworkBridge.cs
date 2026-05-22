using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using BeatDodger.Network;
using BeatDodger.Managers;

namespace BeatDodger.Multiplayer
{
    /// <summary>
    /// 인게임 서버 권한 오브젝트.
    ///
    /// ■ 파티 격리 방식 (핵심 설계 결정)
    /// ─────────────────────────────────────────────────────────────────
    /// 하나의 씬에 여러 MatchSession이 동시에 존재할 때 격리 방법은 두 가지다.
    ///
    /// 방법 A — SessionCoordinator.SendToMatch<T>(matchId, msg)
    ///   conn.Send() 루프로 해당 파티 4명에게만 전송.
    ///   게임 시작, 노트 스폰, 피버, 게임 종료 등 "파티 전체" 데이터에 사용.
    ///
    /// 방법 B — [TargetRpc] on GamePlayer
    ///   지정된 conn 1명에게만 전송.
    ///   판정·콤보 등 "개인" 데이터에 사용.
    ///
    /// [ClientRpc]는 절대 사용하지 않는다.
    ///   → 씬 내 모든 클라이언트에게 브로드캐스트되어 파티 격리가 파괴된다.
    /// ─────────────────────────────────────────────────────────────────
    ///
    /// ■ 피버 게이지 설계 [추가]
    ///   파티 4인이 공유하는 단일 MatchFeverGauge를 사용한다. (개인별 PlayerFeverGauges 제거)
    ///   누구든 히트 시 게이지 증가, 미스 시 감소.
    ///   100% 도달 시 FeverSequenceCoroutine으로 보스전 진행.
    ///
    /// ■ 보스전 흐름 [추가]
    ///   FeverSuccessCount &lt; 2  : 보스 FallDown + 버튼 연타 (mashingDuration, requiredMashCount)
    ///   FeverSuccessCount >= 2 : 레이저 결투 — 연타로 클래시 포인트 전진, 보스가 bossPushSpeed로 밀어냄
    ///   레이저 결투 성공 시 GameCleared = true → EndMatch(Cleared)
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class GameNetworkBridge : NetworkBehaviour
    {
        public static GameNetworkBridge Instance { get; private set; }

        private readonly Dictionary<int, Queue<ScheduledNote>> _noteQueues
            = new Dictionary<int, Queue<ScheduledNote>>();

        private readonly Dictionary<int, MatchGameState> _matchStates
            = new Dictionary<int, MatchGameState>();

        private readonly Dictionary<uint, GamePlayer> _gamePlayers
            = new Dictionary<uint, GamePlayer>();

        private int _globalNoteId = 0;

        [Header("Game Settings")]
        [SerializeField] private float _startDelaySeconds = 3f;

        [Header("Fever / Boss Fight Settings")]
        [SerializeField] private float _mashingDuration = 10f;
        [SerializeField] private float _requiredMashCount = 50f;
        [SerializeField] private float _enterAnimDuration = 1.5f;
        [SerializeField] private float _exitAnimDuration = 1.5f;
        [SerializeField] private float _laserDuelRequiredMashCount = 80f;
        [SerializeField] private float _laserDuelDuration = 8f;
        [SerializeField] private float _bossPushSpeed = 10f;
        [SerializeField] private float _feverUpdateInterval = 0.05f;

        [SerializeField] private SessionCoordinator _sessionCoordinator;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_sessionCoordinator == null)
                _sessionCoordinator = FindFirstObjectByType<SessionCoordinator>();

            // [추가] 피버 연타 입력 수신 등록 (클라이언트가 SubmitMashMessage를 직접 전송)
            NetworkServer.RegisterHandler<SubmitMashMessage>(OnSubmitMash);
        }

        // ───────────────────────────────────────────────────────────────
        // 서버 공개 API
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 새 매치를 초기화하고 게임 시작 메시지를 파티 전원에게 전송한다.
        /// chart가 null이면 랜덤 패턴 생성, 아니면 RhythmChart ScriptableObject를 사용한다.
        /// [원복] 재작성 시 소실된 원본 주석 복원
        /// </summary>
        [Server]
        public void StartMatch(int matchId, int songId, string songName, int difficulty,
            float bpm, int beatsToArrive, RhythmChart chart = null)
        {
            if (_matchStates.ContainsKey(matchId))
            {
                Debug.LogWarning($"[GameNetworkBridge] 이미 시작된 매치 — MatchId: {matchId}");
                return;
            }

            double serverStartDspTime = AudioSettings.dspTime + _startDelaySeconds;

            var state = new MatchGameState
            {
                MatchId            = matchId,
                Bpm                = bpm,
                BeatsToArrive      = beatsToArrive,
                ServerStartDspTime = serverStartDspTime,
                IsActive           = true,
                HitNoteIds         = new HashSet<int>(),
                PlayerCombos       = new Dictionary<uint, int>(),
                MatchFeverGauge    = 0f,
                FeverSuccessCount  = 0,
                IsFeverActive      = false,
                IsNoteSpawnPaused  = false,
            };
            _matchStates[matchId] = state;

            var queue = BuildNoteQueue(matchId, bpm, beatsToArrive, serverStartDspTime, chart);
            _noteQueues[matchId] = queue;

            var participants = _sessionCoordinator?.GetMatchParticipants(matchId);
            if (participants == null)
            {
                Debug.LogError($"[GameNetworkBridge] 참가자 목록 조회 실패 — MatchId: {matchId}");
                return;
            }

            for (int i = 0; i < participants.Count; i++)
            {
                var conn = participants[i];
                conn.Send(new GameStartMessage
                {
                    MatchId            = matchId,
                    SongId             = songId,
                    SongName           = songName,
                    Difficulty         = difficulty,
                    ServerStartDspTime = serverStartDspTime,
                    Bpm                = bpm,
                    BeatsToArrive      = beatsToArrive,
                    SlotIndex          = i,
                });

                if (conn.identity != null && conn.identity.TryGetComponent(out GamePlayer gp))
                {
                    gp.SetMatchInfo(matchId, i);
                    _gamePlayers[conn.identity.netId] = gp;
                    state.PlayerCombos[conn.identity.netId] = 0;
                }
            }

            StartCoroutine(NoteSpawnLoop(matchId));
            Debug.Log($"[GameNetworkBridge] 매치 시작 — MatchId: {matchId}, 참가자: {participants.Count}명");
        }

        /// <summary>
        /// 클라이언트(GamePlayer.CmdHitNote)로부터 노트 히트를 수신하고 판정을 처리한다.
        /// 판정 결과를 TargetRpc(개인)와 SendToMatch(파티 전체)로 이중 전송한다.
        /// [원복] 재작성 시 소실된 원본 주석 복원
        /// </summary>
        [Server]
        public void ProcessHitNote(NetworkConnectionToClient conn, int matchId,
            int noteId, double dspHitTime, uint hitterNetId)
        {
            if (!_matchStates.TryGetValue(matchId, out MatchGameState state)) return;
            if (!state.IsActive || state.IsFeverActive) return;

            if (state.HitNoteIds.Contains(noteId)) return;
            state.HitNoteIds.Add(noteId);

            double targetHitTime = GetNoteHitTime(matchId, noteId);
            double offset = Math.Abs(dspHitTime - targetHitTime);
            Judgment judgment = EvaluateJudgment(offset);

            // 콤보 갱신 (개인)
            if (judgment == Judgment.Miss)
                state.PlayerCombos[hitterNetId] = 0;
            else
                state.PlayerCombos[hitterNetId] = state.PlayerCombos.GetValueOrDefault(hitterNetId) + 1;

            // 피버 게이지 갱신 (파티 공유)
            if (judgment == Judgment.Miss)
                state.MatchFeverGauge = Mathf.Max(0f, state.MatchFeverGauge - 5f);
            else
                state.MatchFeverGauge = Mathf.Min(100f, state.MatchFeverGauge + 10f);

            int combo = state.PlayerCombos[hitterNetId];
            float sharedFever = state.MatchFeverGauge;

            // ① TargetRpc — 히트한 플레이어 개인에게 판정·콤보 전송
            if (_gamePlayers.TryGetValue(hitterNetId, out GamePlayer gp))
                gp.TargetReceiveJudgment(conn, noteId, (int)judgment, combo, sharedFever);

            // ② SendToMatch — 파티 전체에 히트 결과 + 공유 피버 게이지 브로드캐스트
            _sessionCoordinator?.SendToMatch(matchId, new NoteHitResultMessage
            {
                MatchId      = matchId,
                NoteId       = noteId,
                HitterNetId  = hitterNetId,
                Judgment     = judgment,
                Combo        = combo,
                FeverGauge   = sharedFever,
            });

            // 피버 게이지 100% 도달 시 보스전 시작
            if (!state.IsFeverActive && state.MatchFeverGauge >= 100f)
                TriggerFever(matchId, state);
        }

        /// <summary>
        /// 모든 플레이어 연결 해제 등 외부에서 강제 종료가 필요할 때 호출한다.
        /// PartyNetworkBridge 등 상위 시스템이 매치 비정상 종료 시 이 메서드를 사용한다.
        /// </summary>
        [Server]
        public void ForceEndMatch(int matchId)
        {
            EndMatch(matchId, GameEndReason.AllDisconnected);
        }

        // ───────────────────────────────────────────────────────────────
        // 피버 / 보스전
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 공유 피버 게이지가 100%에 도달했을 때 보스전 시퀀스를 시작한다.
        /// FeverSuccessCount에 따라 연타 또는 레이저 결투 모드를 결정하고
        /// FeverStartMessage를 파티 전원에게 전송한 뒤 FeverSequenceCoroutine을 시작한다.
        /// [추가]
        /// </summary>
        [Server]
        private void TriggerFever(int matchId, MatchGameState state)
        {
            state.IsFeverActive     = true;
            state.IsNoteSpawnPaused = true;
            state.MatchFeverGauge   = 0f;
            state.PendingMashCount  = 0;

            bool isLaserDuel      = state.FeverSuccessCount >= 2;
            float duration        = isLaserDuel ? _laserDuelDuration    : _mashingDuration;
            float targetMash      = isLaserDuel ? _laserDuelRequiredMashCount : _requiredMashCount;
            float initialMash     = isLaserDuel ? targetMash * 0.5f     : 0f;
            state.CurrentMashFloat = initialMash;

            _sessionCoordinator?.SendToMatch(matchId, new FeverStartMessage
            {
                MatchId        = matchId,
                FeverStage     = state.FeverSuccessCount + 1,
                Duration       = duration,
                TargetMashCount = targetMash,
                InitialMashFloat = initialMash,
            });

            StartCoroutine(FeverSequenceCoroutine(matchId, state, isLaserDuel, duration, targetMash));
        }

        /// <summary>
        /// 클라이언트가 피버 연타 중 레인 키를 누를 때마다 수신하는 핸들러.
        /// PendingMashCount를 누적하며, FeverSequenceCoroutine이 매 프레임 소비한다.
        /// [추가]
        /// </summary>
        [Server]
        private void OnSubmitMash(NetworkConnectionToClient conn, SubmitMashMessage msg)
        {
            if (!_matchStates.TryGetValue(msg.MatchId, out MatchGameState state)) return;
            if (!state.IsFeverActive) return;
            state.PendingMashCount++;
        }

        /// <summary>
        /// 피버 보스전 전체 흐름을 관장하는 코루틴. NewRhythmManager.FeverSequenceCoroutine과 동일한 구조.
        /// 진입 애니메이션 대기 → 연타/레이저 루프 → 결과 전송 → 종료 애니메이션 대기 → 노트 재개.
        /// [추가]
        /// </summary>
        private IEnumerator FeverSequenceCoroutine(int matchId, MatchGameState state,
            bool isLaserDuel, float duration, float targetMash)
        {
            // 진입 애니메이션 대기 (클라이언트 측 애니메이션과 동기화)
            yield return new WaitForSeconds(_enterAnimDuration);

            float elapsed     = 0f;
            float updateTimer = 0f;

            while (elapsed < duration)
            {
                if (!_matchStates.ContainsKey(matchId)) yield break;

                int newMashes          = state.PendingMashCount;
                state.PendingMashCount = 0;

                if (isLaserDuel)
                {
                    state.CurrentMashFloat -= _bossPushSpeed * Time.deltaTime;
                    state.CurrentMashFloat += newMashes;
                    state.CurrentMashFloat = Mathf.Clamp(state.CurrentMashFloat, 0f, targetMash);
                }
                else
                {
                    state.CurrentMashFloat += newMashes;
                }

                float progress = state.CurrentMashFloat / targetMash;

                updateTimer += Time.deltaTime;
                if (updateTimer >= _feverUpdateInterval)
                {
                    updateTimer = 0f;
                    _sessionCoordinator?.SendToMatch(matchId, new FeverUpdateMessage
                    {
                        MatchId       = matchId,
                        Progress      = progress,
                        TimeRemaining = duration - elapsed,
                    });
                }

                if (state.CurrentMashFloat >= targetMash)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            bool success    = state.CurrentMashFloat >= targetMash;
            bool gameCleared = false;

            if (success)
            {
                state.FeverSuccessCount++;
                if (isLaserDuel) gameCleared = true;
            }

            // 최종 업데이트 전송
            _sessionCoordinator?.SendToMatch(matchId, new FeverUpdateMessage
            {
                MatchId       = matchId,
                Progress      = success ? 1f : state.CurrentMashFloat / targetMash,
                TimeRemaining = 0f,
            });

            _sessionCoordinator?.SendToMatch(matchId, new FeverResultMessage
            {
                MatchId          = matchId,
                Success          = success,
                GameCleared      = gameCleared,
                FeverSuccessCount = state.FeverSuccessCount,
            });

            // 종료 애니메이션 대기
            yield return new WaitForSeconds(_exitAnimDuration);

            // 피버 중 지나간 노트 제거 (재개 시 노트 버스트 방지)
            if (_noteQueues.TryGetValue(matchId, out Queue<ScheduledNote> q))
            {
                double now = AudioSettings.dspTime;
                while (q.Count > 0 && q.Peek().HitTime < now)
                    q.Dequeue();
            }

            state.IsFeverActive     = false;
            state.IsNoteSpawnPaused = false;

            if (gameCleared)
                EndMatch(matchId, GameEndReason.Cleared);
        }

        // ───────────────────────────────────────────────────────────────
        // 내부 구현
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// 서버 시작 DSP 시간에 맞춰 노트를 스케줄대로 파티 전원에게 전송하는 코루틴.
        /// 피버 중에는 IsNoteSpawnPaused가 true인 동안 대기하고, 큐가 비면 게임 클리어 처리.
        /// [원복] 재작성 시 소실된 원본 주석 복원
        /// </summary>
        private IEnumerator NoteSpawnLoop(int matchId)
        {
            if (!_matchStates.TryGetValue(matchId, out MatchGameState state)) yield break;
            if (!_noteQueues.TryGetValue(matchId, out Queue<ScheduledNote> queue)) yield break;

            while (AudioSettings.dspTime < state.ServerStartDspTime)
                yield return null;

            while (state.IsActive && queue.Count > 0)
            {
                // 피버 중에는 노트 스폰 일시 정지
                while (state.IsNoteSpawnPaused)
                    yield return null;

                ScheduledNote next       = queue.Peek();
                double noteDuration      = (60.0 / state.Bpm) * state.BeatsToArrive;
                double spawnSendTime     = next.HitTime - noteDuration;

                if (AudioSettings.dspTime >= spawnSendTime)
                {
                    queue.Dequeue();
                    _sessionCoordinator?.SendToMatch(matchId, new NoteSpawnMessage
                    {
                        MatchId  = matchId,
                        NoteId   = next.NoteId,
                        Lane     = next.Lane,
                        Span     = next.Span,
                        NoteType = next.Type,
                        HitTime  = next.HitTime,
                    });
                }

                yield return null;
            }

            if (state.IsActive)
            {
                yield return new WaitForSeconds(3f);
                EndMatch(matchId, GameEndReason.Cleared);
            }
        }

        /// <summary>
        /// 매치를 종료하고 파티 전원에게 GameEndMessage를 전송한 뒤 상태를 정리한다.
        /// _noteHitTimes에서 해당 매치 노트 ID들을 제거해 메모리 누수를 방지한다.
        /// [원복] 재작성 시 소실된 원본 주석 복원
        /// </summary>
        [Server]
        private void EndMatch(int matchId, GameEndReason reason)
        {
            if (!_matchStates.TryGetValue(matchId, out MatchGameState state)) return;
            state.IsActive = false;

            _sessionCoordinator?.SendToMatch(matchId, new GameEndMessage
            {
                MatchId = matchId,
                Reason  = reason,
            });

            _matchStates.Remove(matchId);
            _noteQueues.Remove(matchId);

            if (_matchNoteIds.TryGetValue(matchId, out HashSet<int> noteIds))
            {
                foreach (int id in noteIds)
                    _noteHitTimes.Remove(id);
                _matchNoteIds.Remove(matchId);
            }

            Debug.Log($"[GameNetworkBridge] 매치 종료 — MatchId: {matchId}, 사유: {reason}");
        }

        /// <summary>
        /// 매치용 노트 큐를 생성한다.
        /// chart가 있으면 차트 데이터를 순서대로, 없으면 matchId를 시드로 한 랜덤 패턴을 생성한다.
        /// [원복] 재작성 시 소실된 원본 주석 복원
        /// </summary>
        private Queue<ScheduledNote> BuildNoteQueue(int matchId, float bpm, int beatsToArrive,
            double startDspTime, RhythmChart chart)
        {
            var queue     = new Queue<ScheduledNote>();
            var noteIdSet = new HashSet<int>();
            _matchNoteIds[matchId] = noteIdSet;

            float secondsPerBeat = 60f / bpm;

            if (chart != null)
            {
                foreach (NoteData nd in chart.notes)
                {
                    int noteId    = _globalNoteId++;
                    double hitTime = startDspTime + nd.time;
                    _noteHitTimes[noteId] = hitTime;
                    noteIdSet.Add(noteId);
                    queue.Enqueue(new ScheduledNote
                    {
                        NoteId = noteId,
                        Lane   = nd.lane,
                        Span   = nd.span,
                        Type   = nd.type,
                        HitTime = hitTime,
                    });
                }
            }
            else
            {
                int totalBeats     = Mathf.RoundToInt(60f / secondsPerBeat * 60f);
                System.Random rng  = new System.Random(matchId);
                for (int beat = beatsToArrive; beat < totalBeats; beat++)
                {
                    int noteId    = _globalNoteId++;
                    double hitTime = startDspTime + beat * secondsPerBeat;
                    _noteHitTimes[noteId] = hitTime;
                    noteIdSet.Add(noteId);
                    int lane = rng.Next(0, RhythmConfig.Instance != null
                        ? RhythmConfig.Instance.LaneCount : 4);
                    queue.Enqueue(new ScheduledNote
                    {
                        NoteId = noteId,
                        Lane   = lane,
                        Span   = 1,
                        Type   = NoteType.Normal,
                        HitTime = hitTime,
                    });
                }
            }

            return queue;
        }

        private readonly Dictionary<int, double>      _noteHitTimes = new Dictionary<int, double>();
        private readonly Dictionary<int, HashSet<int>> _matchNoteIds = new Dictionary<int, HashSet<int>>();

        /// <summary>노트 ID로 예정 히트 시간을 조회한다. 없으면 현재 dspTime 반환. [원복]</summary>
        private double GetNoteHitTime(int matchId, int noteId)
            => _noteHitTimes.TryGetValue(noteId, out double t) ? t : AudioSettings.dspTime;

        /// <summary>
        /// dsp 타임 오프셋을 RhythmConfig 임계값과 비교해 판정을 반환한다.
        /// RhythmConfig가 없으면 하드코딩된 기본값(50/100/150ms)을 사용한다.
        /// [원복] 재작성 시 소실된 원본 주석 복원
        /// </summary>
        private Judgment EvaluateJudgment(double offset)
        {
            RhythmConfig cfg = RhythmConfig.Instance;
            if (cfg == null)
            {
                return offset < 0.05 ? Judgment.Perfect :
                       offset < 0.10 ? Judgment.Great   :
                       offset < 0.15 ? Judgment.Good    : Judgment.Miss;
            }
            if (offset <= cfg.PerfectThreshold) return Judgment.Perfect;
            if (offset <= cfg.GreatThreshold)   return Judgment.Great;
            if (offset <= cfg.GoodThreshold)    return Judgment.Good;
            return Judgment.Miss;
        }

        // ───────────────────────────────────────────────────────────────
        // 내부 데이터 구조
        // ───────────────────────────────────────────────────────────────

        private class MatchGameState
        {
            public int    MatchId;
            public float  Bpm;
            public int    BeatsToArrive;
            public double ServerStartDspTime;
            public bool   IsActive;
            public HashSet<int>         HitNoteIds;
            public Dictionary<uint, int> PlayerCombos;

            // 파티 공유 피버 게이지 [추가] — 개인 PlayerFeverGauges를 대체, 히트/미스 시 전체 반영
            public float MatchFeverGauge;

            // 보스전 상태 [추가]
            public int   FeverSuccessCount;
            public bool  IsFeverActive;
            public float CurrentMashFloat;
            public int   PendingMashCount;
            public bool  IsNoteSpawnPaused;  // true 동안 NoteSpawnLoop 일시정지
        }

        private struct ScheduledNote
        {
            public int      NoteId;
            public int      Lane;
            public int      Span;
            public NoteType Type;
            public double   HitTime;
        }
    }
}
