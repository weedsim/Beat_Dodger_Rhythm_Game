using UnityEngine;
using Mirror;

namespace BeatDodger.Multiplayer
{
    /// <summary>
    /// 인게임에서 플레이어 1인당 스폰되는 NetworkBehaviour.
    ///
    /// 역할:
    ///  - 클라이언트 → 서버: [Command]로 히트 입력 전송
    ///  - 서버 → 개인: [TargetRpc]로 판정 결과 수신 (파티 격리 보장)
    ///
    /// [TargetRpc] vs [ClientRpc]:
    ///  - [ClientRpc]는 서버 씬에 존재하는 모든 클라이언트가 받음 → 파티 격리 불가
    ///  - [TargetRpc]는 지정된 conn 한 명에게만 전송 → 판정·콤보·피버게이지 개인 데이터에 적합
    /// </summary>
    public class GamePlayer : NetworkBehaviour
    {
        [SyncVar] private int _matchId = -1;
        [SyncVar] private int _slotIndex = -1;

        public int MatchId => _matchId;
        public int SlotIndex => _slotIndex;

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            GameSessionContext.Instance?.RegisterLocalGamePlayer(this);
            Debug.Log($"[GamePlayer] Local player started — NetId: {netId}");
        }

        /// <summary>
        /// 서버에서 스폰 직후 매치 정보를 설정한다. 서버 전용.
        /// </summary>
        [Server]
        public void SetMatchInfo(int matchId, int slotIndex)
        {
            _matchId = matchId;
            _slotIndex = slotIndex;
        }

        // ---------------------------------------------------------------
        // Client → Server
        // ---------------------------------------------------------------

        /// <summary>
        /// 노트를 히트했음을 서버에 보고한다.
        /// </summary>
        /// <param name="matchId">현재 매치 ID (서버측 검증용)</param>
        /// <param name="noteId">히트한 노트 고유 ID</param>
        /// <param name="dspHitTime">히트 시점의 AudioSettings.dspTime</param>
        [Command]
        public void CmdHitNote(int matchId, int noteId, double dspHitTime)
        {
            if (_matchId != matchId)
            {
                Debug.LogWarning($"[GamePlayer] [Server] CmdHitNote matchId 불일치 — 기대: {_matchId}, 수신: {matchId}");
                return;
            }

            GameNetworkBridge.Instance?.ProcessHitNote(connectionToClient, matchId, noteId, dspHitTime, netId);
        }

        // ---------------------------------------------------------------
        // Server → This Player Only (TargetRpc)
        // TargetRpc는 지정된 conn에게만 전송되므로 파티 내 다른 플레이어도 받지 않는다.
        // ---------------------------------------------------------------

        /// <summary>
        /// 서버가 계산한 판정 결과를 이 플레이어에게만 전달한다.
        /// </summary>
        [TargetRpc]
        public void TargetReceiveJudgment(NetworkConnectionToClient target,
            int noteId, int judgment, int combo, float feverGauge)
        {
            Judgment j = (Judgment)judgment;
            Debug.Log($"[GamePlayer] 판정 수신 — NoteId: {noteId}, {j}, 콤보: {combo}, 피버: {feverGauge:F0}%");
            MultiplayerRhythmManager.Instance?.OnPersonalJudgmentReceived(noteId, j, combo, feverGauge);
        }

        /// <summary>
        /// 게임 오버(개인) 또는 특수 이벤트를 이 플레이어에게만 전달한다.
        /// </summary>
        [TargetRpc]
        public void TargetReceivePersonalEvent(NetworkConnectionToClient target, string eventKey)
        {
            Debug.Log($"[GamePlayer] 개인 이벤트 수신 — {eventKey}");
            MultiplayerRhythmManager.Instance?.OnPersonalEventReceived(eventKey);
        }
    }
}
