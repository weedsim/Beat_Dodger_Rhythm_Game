using System.Collections.Generic;
using BeatDodger.Core;
using UnityEngine;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 파티 생성/참가/해제 비즈니스 로직을 담당하는 순수 C# 서비스 클래스.
    /// SyncList 갱신은 PartyNetworkBridge에 위임하여 결과만 콜백으로 전달한다.
    /// </summary>
    public class PartyService
    {
        #region Variables

        private readonly PartyRepository _repository;
        private readonly int _maxParties;

        #endregion

        #region Constructor

        /// <summary>
        /// PartyService 생성자.
        /// </summary>
        /// <param name="repository">파티 데이터 저장소</param>
        /// <param name="maxParties">허용할 최대 파티 수</param>
        public PartyService(PartyRepository repository, int maxParties)
        {
            _repository = repository;
            _maxParties = maxParties;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 새로운 파티를 생성한다. 최대 파티 수 초과 시 null을 반환한다.
        /// </summary>
        /// <param name="roomName">방 이름</param>
        /// <param name="difficulty">난이도</param>
        /// <param name="leaderNetId">방장의 NetId</param>
        /// <returns>생성된 PartyInfo, 실패 시 null</returns>
        public PartyInfo? CreateParty(string roomName, int difficulty, uint leaderNetId)
        {
            if (_repository.Count() >= _maxParties)
            {
                Debug.LogWarning("[PartyService] [Server] 최대 파티 수에 도달하여 파티 생성에 실패했습니다.");
                return null;
            }

            PartyInfo created = _repository.CreateParty(roomName, difficulty, leaderNetId);

            Debug.Log($"[PartyService] [Server] 파티 생성 완료 | ID: {created._PartyId} | 방 이름: {created._RoomName}" +
                      $" | 방장 NetId: {created._Slot0NetId}");

            return created;
        }

        /// <summary>
        /// 특정 파티에 플레이어를 참가시킨다. 성공 시 갱신된 PartyInfo를 반환한다.
        /// </summary>
        /// <param name="partyId">참가할 파티 ID</param>
        /// <param name="joinerNetId">참가자의 NetId</param>
        /// <returns>갱신된 PartyInfo, 실패 시 null</returns>
        public PartyInfo? JoinParty(int partyId, uint joinerNetId)
        {
            if (_repository.TryJoinParty(partyId, joinerNetId, out PartyInfo updated))
            {
                Debug.Log($"[PartyService] [Server] 파티 참가 완료 | ID: {partyId} | 참가자 NetId: {joinerNetId}");
                return updated;
            }

            Debug.LogWarning($"[PartyService] [Server] 파티 참가 실패 | ID: {partyId} | 참가자 NetId: {joinerNetId}");
            return null;
        }

        /// <summary>
        /// 플레이어 접속 해제 시 해당 플레이어가 속한 파티 데이터를 정리한다.
        /// 변경된 파티 목록을 반환하므로 호출자가 SyncList를 갱신해야 한다.
        /// </summary>
        /// <param name="disconnectedNetId">접속이 끊긴 플레이어의 NetId</param>
        /// <returns>변경된 PartyInfo 목록 (SyncList 갱신 대상)</returns>
        public List<PartyInfo> HandlePlayerDisconnect(uint disconnectedNetId)
        {
            _repository.HandlePlayerDisconnect(disconnectedNetId, out List<PartyInfo> updatedParties);
            return updatedParties;
        }

        /// <summary>
        /// 특정 파티에서 플레이어의 악기를 선택하거나 취소한다. 중복 선택 시 null을 반환한다.
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="netId">악기를 선택하는 플레이어의 NetId</param>
        /// <param name="instrument">선택할 악기. None이면 선택 취소.</param>
        /// <returns>갱신된 PartyInfo, 실패 시 null</returns>
        public PartyInfo? SelectInstrument(int partyId, uint netId, InstrumentType instrument)
        {
            if (_repository.TrySelectInstrument(partyId, netId, instrument, out PartyInfo updated))
            {
                Debug.Log($"[PartyService] [Server] 악기 선택 완료 | PartyId: {partyId} | NetId: {netId} | 악기: {instrument}");
                return updated;
            }

            Debug.LogWarning($"[PartyService] [Server] 악기 선택 실패 | PartyId: {partyId} | NetId: {netId} | 악기: {instrument}");
            return null;
        }

        /// <summary>
        /// 특정 파티에서 플레이어의 준비 상태를 변경한다. 방장 또는 미소속 시 null을 반환한다.
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="netId">준비 상태를 변경할 플레이어의 NetId</param>
        /// <param name="ready">설정할 준비 상태</param>
        /// <returns>갱신된 PartyInfo, 실패 시 null</returns>
        public PartyInfo? SetReady(int partyId, uint netId, bool ready)
        {
            if (_repository.TrySetReady(partyId, netId, ready, out PartyInfo updated))
            {
                Debug.Log($"[PartyService] [Server] 준비 상태 변경 완료 | PartyId: {partyId} | NetId: {netId} | Ready: {ready}");
                return updated;
            }

            Debug.LogWarning($"[PartyService] [Server] 준비 상태 변경 실패 | PartyId: {partyId} | NetId: {netId} | Ready: {ready}");
            return null;
        }

        /// <summary>
        /// 파티가 게임을 시작할 수 있는 조건인지 검사한다.
        /// 조건: 4인 풀방, Slot1~3 모두 준비 완료, 전원(Slot0~3) 악기 선택 완료.
        /// </summary>
        /// <param name="partyId">검사할 파티 ID</param>
        /// <param name="party">조건 충족 시 해당 PartyInfo를 담는 out 파라미터</param>
        /// <returns>시작 가능 여부</returns>
        public bool CanStartMatch(int partyId, out PartyInfo party)
        {
            IReadOnlyList<PartyInfo> all = _repository.GetAll();
            int count = all.Count;

            for (int i = 0; i < count; i++)
            {
                PartyInfo candidate = all[i];
                if (!candidate._IsActive || candidate._PartyId != partyId)
                {
                    continue;
                }

                // 4인 풀방 조건
                if (candidate._MemberCount != PartyConstants.MAX_PARTY_MEMBERS)
                {
                    party = candidate;
                    return false;
                }

                // Slot1~3 준비 완료 조건
                for (int slot = 1; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                {
                    if (!candidate.GetReady(slot))
                    {
                        party = candidate;
                        return false;
                    }
                }

                // 전원 악기 선택 완료 조건
                for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                {
                    if (candidate.GetInstrument(slot) == InstrumentType.None)
                    {
                        party = candidate;
                        return false;
                    }
                }

                party = candidate;
                return true;
            }

            party = default;
            return false;
        }

        /// <summary>
        /// 활성화된 파티 목록을 가져와 매개변수로 전달된 리스트에 채운다.
        /// </summary>
        /// <param name="outParties">결과를 담을 미리 할당된 리스트 (Zero-GC 패턴)</param>
        public void GetActiveParties(List<PartyInfo> outParties)
        {
            outParties.Clear();
            IReadOnlyList<PartyInfo> all = _repository.GetAll();
            int count = all.Count;

            for (int i = 0; i < count; i++)
            {
                if (all[i]._IsActive)
                {
                    outParties.Add(all[i]);
                }
            }
        }

        #endregion
    }
}
