using System.Collections.Generic;
using BeatDodger.Core;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 서버 측 파티 데이터 저장소. 순수 C# 클래스로 SyncList와 무관하게 내부 상태를 관리한다.
    /// 비밀번호는 보안상 SyncList에 포함하지 않고 이 저장소에서만 관리한다.
    /// </summary>
    public class PartyRepository
    {
        #region Variables

        private readonly List<PartyInfo> _parties = new List<PartyInfo>();
        private readonly Dictionary<int, string> _partyPasswords = new Dictionary<int, string>();
        private int _partyIdCounter = 1;

        #endregion

        #region Public Methods

        /// <summary>
        /// 새 파티를 저장소에 추가하고 생성된 PartyInfo를 반환한다.
        /// </summary>
        /// <param name="roomName">방 이름</param>
        /// <param name="password">방 비밀번호. 빈 문자열이면 공개방으로 생성된다.</param>
        /// <param name="songName">방장이 선택한 곡 이름</param>
        /// <param name="leaderNetId">방장의 NetId (Slot0에 배치)</param>
        /// <returns>생성된 PartyInfo 구조체</returns>
        public PartyInfo CreateParty(string roomName, string password, int songId, string songName, uint leaderNetId)
        {
            bool hasPassword = !string.IsNullOrEmpty(password);
            int partyId = _partyIdCounter++;

            PartyInfo newParty = new PartyInfo
            {
                _IsActive = true,
                _PartyId = partyId,
                _RoomName = roomName,
                _SongName = songName,
                _SongId = songId,
                _Difficulty = 1,
                _HasPassword = hasPassword,
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

            if (hasPassword)
            {
                _partyPasswords[partyId] = password;
            }

            return newParty;
        }

        /// <summary>
        /// 지정한 파티에 멤버를 추가한다. 성공 시 갱신된 PartyInfo를 outParty에 담아 true를 반환한다.
        /// 비밀번호가 설정된 방의 경우 password가 일치하지 않으면 false를 반환한다.
        /// </summary>
        /// <param name="partyId">참가할 파티 ID</param>
        /// <param name="password">입력한 비밀번호. 공개방이면 빈 문자열을 전달한다.</param>
        /// <param name="joinerNetId">참가자의 NetId</param>
        /// <param name="outParty">갱신된 PartyInfo (성공 시)</param>
        /// <returns>참가 성공 여부</returns>
        public bool TryJoinParty(int partyId, string password, uint joinerNetId, out PartyInfo outParty)
        {
            for (int i = 0; i < _parties.Count; i++)
            {
                PartyInfo party = _parties[i];
                if (!party._IsActive || party._PartyId != partyId)
                {
                    continue;
                }

                // 비밀번호 검증 (서버에서만 실제 비밀번호를 알고 있음)
                if (_partyPasswords.TryGetValue(partyId, out string storedPassword))
                {
                    if (storedPassword != password)
                    {
                        outParty = party;
                        return false;
                    }
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

                // 방장(Slot0)이 나간 경우 파티 비활성화 및 비밀번호 정리
                if (party._Slot0NetId == disconnectedNetId)
                {
                    party._IsActive = false;
                    _partyPasswords.Remove(party._PartyId);
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
                // 방장(Slot0)은 GetReady(0)이 항상 true이므로 SetReady 호출 불필요
                if (playerSlot != 0)
                {
                    party.SetReady(playerSlot, instrument != InstrumentType.None);
                }
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
        /// 방장이 대기실에서 곡을 변경한다. 방장(Slot0)만 변경할 수 있다.
        /// </summary>
        /// <param name="partyId">대상 파티 ID</param>
        /// <param name="songId">변경할 곡 고유 ID</param>
        /// <param name="songName">변경할 곡 이름</param>
        /// <param name="difficulty">변경할 곡 난이도</param>
        /// <param name="tags">변경할 곡 해시태그 분위기 문자열</param>
        /// <param name="requestingNetId">요청한 플레이어의 NetId (방장 검증용)</param>
        /// <param name="outParty">갱신된 PartyInfo (성공 시)</param>
        /// <returns>곡 변경 성공 여부</returns>
        public bool TryUpdateSong(int partyId, int songId, string songName, int difficulty, string tags, uint requestingNetId, out PartyInfo outParty)
        {
            for (int i = 0; i < _parties.Count; i++)
            {
                PartyInfo party = _parties[i];
                if (!party._IsActive || party._PartyId != partyId)
                {
                    continue;
                }

                if (party._Slot0NetId != requestingNetId)
                {
                    outParty = party;
                    return false;
                }

                party._SongId = songId;
                party._SongName = songName;
                party._Difficulty = difficulty;
                party._SongTags = tags;
                _parties[i] = party;
                outParty = party;
                return true;
            }

            outParty = default;
            return false;
        }

        /// <summary>
        /// 매치 시작 시 파티 비밀번호를 저장소에서 즉시 제거한다.
        /// 비활성화는 SyncList를 통해 PartyNetworkBridge에서 처리한다.
        /// </summary>
        /// <param name="partyId">정리할 파티 ID</param>
        public void RemovePasswordEntry(int partyId)
        {
            _partyPasswords.Remove(partyId);
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
