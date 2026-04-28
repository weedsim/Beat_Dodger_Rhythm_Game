using System;
using BeatDodger.Core;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 파티 하나의 상태 정보를 담는 구조체. Mirror SyncList로 동기화된다.
    ///
    /// [C6 설계 노트]
    /// Mirror SyncList 내부의 struct는 NetworkWriter/NetworkReader로 직렬화된다.
    /// struct 필드에 참조형(uint[] 배열 등)이 포함되면 Mirror Weaver가 올바르게
    /// 처리하지 못하므로, 4개 슬롯을 개별 uint 필드로 유지한다.
    /// index 규칙: Slot0 = 방장(Leader), Slot1~Slot3 = 팀원.
    /// GetSlot/SetSlot 헬퍼를 통해 인덱스 기반 접근을 제공한다.
    /// </summary>
    [Serializable]
    public struct PartyInfo
    {
        public bool _IsActive;
        public int _PartyId;
        public string _RoomName;
        /// <summary>방장이 선택한 곡 이름. 방 목록에 표시된다.</summary>
        public string _SongName;
        /// <summary>방장이 선택한 곡 고유 ID. 인게임 리소스 로드에 사용된다.</summary>
        public int _SongId;
        /// <summary>방장이 선택한 곡 난이도.</summary>
        public int _Difficulty;
        /// <summary>방장이 선택한 곡의 해시태그 분위기 문자열 (예: "#신남 #빠름"). 대기실 전체에 표시된다.</summary>
        public string _SongTags;
        /// <summary>비밀번호 존재 여부 플래그. 실제 비밀번호는 서버(PartyRepository)에서만 관리한다.</summary>
        public bool _HasPassword;

        public int _MemberCount;
        public int _MaxMembers;

        /// <summary>Slot 0 — 방장(Leader) NetId</summary>
        public uint _Slot0NetId;
        /// <summary>Slot 1 — 팀원 NetId</summary>
        public uint _Slot1NetId;
        /// <summary>Slot 2 — 팀원 NetId</summary>
        public uint _Slot2NetId;
        /// <summary>Slot 3 — 팀원 NetId</summary>
        public uint _Slot3NetId;

        /// <summary>Slot 0 — 방장이 선택한 악기</summary>
        public InstrumentType _Slot0Instrument;
        /// <summary>Slot 1 — 팀원이 선택한 악기</summary>
        public InstrumentType _Slot1Instrument;
        /// <summary>Slot 2 — 팀원이 선택한 악기</summary>
        public InstrumentType _Slot2Instrument;
        /// <summary>Slot 3 — 팀원이 선택한 악기</summary>
        public InstrumentType _Slot3Instrument;

        /// <summary>Slot 0 — 방장 준비 상태 (런타임 GetReady(0)은 항상 true 반환)</summary>
        public bool _Slot0Ready;
        /// <summary>Slot 1 — 팀원 준비 상태</summary>
        public bool _Slot1Ready;
        /// <summary>Slot 2 — 팀원 준비 상태</summary>
        public bool _Slot2Ready;
        /// <summary>Slot 3 — 팀원 준비 상태</summary>
        public bool _Slot3Ready;

        /// <summary>
        /// 인덱스(0~3)로 슬롯 NetId를 읽는다. 방장은 index 0.
        /// </summary>
        /// <param name="index">슬롯 인덱스 (0~3)</param>
        /// <returns>해당 슬롯의 NetId. 범위 초과 시 0.</returns>
        public uint GetSlot(int index)
        {
            switch (index)
            {
                case 0: return _Slot0NetId;
                case 1: return _Slot1NetId;
                case 2: return _Slot2NetId;
                case 3: return _Slot3NetId;
                default: return 0;
            }
        }

        /// <summary>
        /// 인덱스(0~3)로 슬롯 NetId를 설정한다. 방장은 index 0.
        /// </summary>
        /// <param name="index">슬롯 인덱스 (0~3)</param>
        /// <param name="netId">설정할 NetId</param>
        public void SetSlot(int index, uint netId)
        {
            switch (index)
            {
                case 0: _Slot0NetId = netId; break;
                case 1: _Slot1NetId = netId; break;
                case 2: _Slot2NetId = netId; break;
                case 3: _Slot3NetId = netId; break;
            }
        }

        /// <summary>
        /// 인덱스(0~3)로 슬롯의 악기 선택을 읽는다. 방장은 index 0.
        /// </summary>
        /// <param name="index">슬롯 인덱스 (0~3)</param>
        /// <returns>해당 슬롯의 InstrumentType. 범위 초과 시 None.</returns>
        public InstrumentType GetInstrument(int index)
        {
            switch (index)
            {
                case 0: return _Slot0Instrument;
                case 1: return _Slot1Instrument;
                case 2: return _Slot2Instrument;
                case 3: return _Slot3Instrument;
                default: return InstrumentType.None;
            }
        }

        /// <summary>
        /// 인덱스(0~3)로 슬롯의 악기 선택을 설정한다. 방장은 index 0.
        /// </summary>
        /// <param name="index">슬롯 인덱스 (0~3)</param>
        /// <param name="instrument">설정할 악기 종류</param>
        public void SetInstrument(int index, InstrumentType instrument)
        {
            switch (index)
            {
                case 0: _Slot0Instrument = instrument; break;
                case 1: _Slot1Instrument = instrument; break;
                case 2: _Slot2Instrument = instrument; break;
                case 3: _Slot3Instrument = instrument; break;
            }
        }

        /// <summary>
        /// 인덱스(0~3)로 슬롯의 준비 상태를 읽는다.
        /// 방장(index 0)은 설계상 항상 true를 반환한다.
        /// </summary>
        /// <param name="index">슬롯 인덱스 (0~3)</param>
        /// <returns>준비 상태. 방장(0)은 항상 true. 범위 초과 시 false.</returns>
        public bool GetReady(int index)
        {
            switch (index)
            {
                case 0: return true;
                case 1: return _Slot1Ready;
                case 2: return _Slot2Ready;
                case 3: return _Slot3Ready;
                default: return false;
            }
        }

        /// <summary>
        /// 인덱스(0~3)로 슬롯의 준비 상태를 설정한다. 방장(index 0)은 설정 불가.
        /// </summary>
        /// <param name="index">슬롯 인덱스 (1~3). 0은 무시된다.</param>
        /// <param name="ready">설정할 준비 상태</param>
        public void SetReady(int index, bool ready)
        {
            switch (index)
            {
                case 1: _Slot1Ready = ready; break;
                case 2: _Slot2Ready = ready; break;
                case 3: _Slot3Ready = ready; break;
            }
        }
    }
}
