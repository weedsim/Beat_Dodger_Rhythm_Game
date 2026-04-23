using System.Collections.Generic;
using BeatDodger.Core;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 서버 측 파티 데이터 저장소. 순수 C# 클래스로 SyncList와 무관하게 내부 상태를 관리한다.
    /// </summary>
    public class PartyRepository
    {
        #region Variables

        private readonly List<PartyInfo> _parties = new List<PartyInfo>();
        private int _partyIdCounter = 1;

        #endregion

        #region Public Methods

        /// <summary>
        /// 새 파티를 저장소에 추가하고 생성된 PartyInfo를 반환한다.
        /// </summary>
        /// <param name="roomName">방 이름</param>
        /// <param name="difficulty">난이도</param>
        /// <param name="leaderNetId">방장의 NetId (Slot0에 배치)</param>
        /// <returns>생성된 PartyInfo 구조체</returns>
        public PartyInfo CreateParty(string roomName, int difficulty, uint leaderNetId)
        {
            PartyInfo newParty = new PartyInfo
            {
                _IsActive = true,
                _PartyId = _partyIdCounter++,
                _RoomName = roomName,
                _Difficulty = difficulty,
                _MaxMembers = PartyConstants.MAX_PARTY_MEMBERS,
                _MemberCount = 1,
                _Slot0NetId = leaderNetId,
                _Slot1NetId = 0,
                _Slot2NetId = 0,
                _Slot3NetId = 0,
                _Slot0Instrument = InstrumentType.None,
                _Slot1Instrument = InstrumentType.None,
                _Slot2Instrument = InstrumentType.None,
                _Slot3Instrument = InstrumentType.None,
                _Slot0Ready = false,
                _Slot1Ready = false,
                _Slot2Ready = false,
                _Slot3Ready = false
            };

            _parties.Add(newParty);
            return newParty;
        }

        /// <summary>
        /// 지정한 파티에 멤버를 추가한다. 성공 시 갱신된 PartyInfo를 outParty에 담아 true를 반환한다.
        /// </summary>
        /// <param name="partyId">참가할 파티 ID</param>
        /// <param name="joinerNetId">참가자의 NetId</param>
        /// <param name="outParty">갱신된 PartyInfo (성공 시)</param>
        /// <returns>참가 성공 여부</returns>
        public bool TryJoinParty(int partyId, uint joinerNetId, out PartyInfo outParty)
        {
            for (int i = 0; i < _parties.Count; i++)
            {
                PartyInfo party = _parties[i];
                if (!party._IsActive || party._PartyId != partyId)
                {
                    continue;
                }

                if (party._MemberCount >= party._MaxMembers)
                {
                    outParty = party;
                    return false;
                }

                // Slot 1~3에서 빈 슬롯 탐색 (Slot 0 = 방장)
                bool slotFilled = false;
                for (int slot = 1; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                {
                    if (party.GetSlot(slot) == 0)
                    {
                        party.SetSlot(slot, joinerNetId);
                        slotFilled = true;
                        break;
                    }
                }

                if (!slotFilled)
                {
                    outParty = party;
                    return false;
                }

                party._MemberCount++;
                _parties[i] = party;
                outParty = party;
                return true;
            }

            outParty = default;
            return false;
        }

        /// <summary>
        /// 플레이어 접속 해제 시 해당 NetId가 속한 파티 데이터를 정리한다.
        /// 방장(Slot0)이 나가면 파티를 비활성화한다.
        /// </summary>
        /// <param name="disconnectedNetId">접속이 끊긴 플레이어의 NetId</param>
        /// <param name="updatedParties">변경된 PartyInfo 목록 (SyncList 갱신용)</param>
        public void HandlePlayerDisconnect(uint disconnectedNetId, out List<PartyInfo> updatedParties)
        {
            updatedParties = new List<PartyInfo>();

            for (int i = 0; i < _parties.Count; i++)
            {
                PartyInfo party = _parties[i];
                if (!party._IsActive)
                {
                    continue;
                }

                bool changed = false;

                // 방장(Slot0)이 나간 경우 파티 비활성화
                if (party._Slot0NetId == disconnectedNetId)
                {
                    party._IsActive = false;
                    changed = true;
                }
                else
                {
                    // 팀원 슬롯(1~3) 검색
                    for (int slot = 1; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                    {
                        if (party.GetSlot(slot) == disconnectedNetId)
                        {
                            party.SetSlot(slot, 0);
                            party._MemberCount--;
                            changed = true;
                            break;
                        }
                    }
                }

                if (changed)
                {
                    _parties[i] = party;
                    updatedParties.Add(party);
                }
            }
        }

        /// <summary>
        /// 특정 파티에서 netId 플레이어의 악기를 선택하거나 취소한다.
        /// 다른 슬롯이 이미 같은 악기를 선택한 경우 중복 방지를 위해 false를 반환한다.
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="netId">악기를 선택하는 플레이어의 NetId</param>
        /// <param name="instrument">선택할 악기. None이면 선택 취소.</param>
        /// <param name="outParty">갱신된 PartyInfo (성공 시)</param>
        /// <returns>악기 선택 성공 여부</returns>
        public bool TrySelectInstrument(int partyId, uint netId, InstrumentType instrument, out PartyInfo outParty)
        {
            for (int i = 0; i < _parties.Count; i++)
            {
                PartyInfo party = _parties[i];
                if (!party._IsActive || party._PartyId != partyId)
                {
                    continue;
                }

                // 이 플레이어가 어느 슬롯에 있는지 확인
                int playerSlot = -1;
                for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                {
                    if (party.GetSlot(slot) == netId)
                    {
                        playerSlot = slot;
                        break;
                    }
                }

                if (playerSlot < 0)
                {
                    outParty = party;
                    return false;
                }

                // None이 아닐 때 다른 슬롯에서 중복 검사
                if (instrument != InstrumentType.None)
                {
                    for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                    {
                        if (slot == playerSlot)
                        {
                            continue;
                        }

                        if (party.GetInstrument(slot) == instrument)
                        {
                            outParty = party;
                            return false;
                        }
                    }
                }

                party.SetInstrument(playerSlot, instrument);
                _parties[i] = party;
                outParty = party;
                return true;
            }

            outParty = default;
            return false;
        }

        /// <summary>
        /// 특정 파티에서 netId 플레이어의 준비 상태를 변경한다.
        /// 방장(Slot0)은 준비 상태 변경 불가로 false를 반환한다.
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="netId">준비 상태를 변경할 플레이어의 NetId</param>
        /// <param name="ready">설정할 준비 상태</param>
        /// <param name="outParty">갱신된 PartyInfo (성공 시)</param>
        /// <returns>준비 상태 변경 성공 여부</returns>
        public bool TrySetReady(int partyId, uint netId, bool ready, out PartyInfo outParty)
        {
            for (int i = 0; i < _parties.Count; i++)
            {
                PartyInfo party = _parties[i];
                if (!party._IsActive || party._PartyId != partyId)
                {
                    continue;
                }

                // 방장(Slot0)은 준비 상태 변경 불가
                if (party._Slot0NetId == netId)
                {
                    outParty = party;
                    return false;
                }

                // Slot1~3에서 해당 netId 탐색
                int playerSlot = -1;
                for (int slot = 1; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
                {
                    if (party.GetSlot(slot) == netId)
                    {
                        playerSlot = slot;
                        break;
                    }
                }

                if (playerSlot < 0)
                {
                    outParty = party;
                    return false;
                }

                party.SetReady(playerSlot, ready);
                _parties[i] = party;
                outParty = party;
                return true;
            }

            outParty = default;
            return false;
        }

        /// <summary>
        /// 현재 저장소의 파티 목록 전체를 반환한다.
        /// </summary>
        /// <returns>파티 목록 (읽기 전용)</returns>
        public IReadOnlyList<PartyInfo> GetAll()
        {
            return _parties;
        }

        /// <summary>
        /// 현재 저장된 파티 수를 반환한다.
        /// </summary>
        /// <returns>파티 수</returns>
        public int Count()
        {
            return _parties.Count;
        }

        #endregion
    }
}
