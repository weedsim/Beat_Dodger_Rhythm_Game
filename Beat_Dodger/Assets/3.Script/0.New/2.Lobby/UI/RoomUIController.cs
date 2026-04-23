using BeatDodger.Core;
using BeatDodger.Lobby;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace BeatDodger.UI
{
    /// <summary>
    /// 파티룸 내부 UI를 관리하는 컨트롤러.
    /// 멤버 슬롯 표시, 악기 선택 버튼, 방 나가기 버튼을 담당한다.
    /// </summary>
    public class RoomUIController : MonoBehaviour
    {
        #region Variables

        [Header("Network Bridge")]
        [SerializeField, Tooltip("파티 네트워크 브릿지")]
        private PartyNetworkBridge _bridge;

        [SerializeField, Tooltip("LobbyUIManager (서브패널 토글용)")]
        private LobbyUIManager _lobbyUIManager;

        [Header("Member Slots")]
        [SerializeField, Tooltip("멤버 이름 텍스트 (4개, Slot0~3 순서)")]
        private Text[] _memberNameTexts;

        [SerializeField, Tooltip("멤버 악기 텍스트 (4개, Slot0~3 순서)")]
        private Text[] _memberInstrumentTexts;

        [Header("Instrument Buttons")]
        [SerializeField, Tooltip("드럼 선택 버튼")]
        private Button _drumButton;

        [SerializeField, Tooltip("기타 선택 버튼")]
        private Button _guitarButton;

        [SerializeField, Tooltip("베이스 선택 버튼")]
        private Button _bassButton;

        [SerializeField, Tooltip("키보드 선택 버튼")]
        private Button _keyboardButton;

        [Header("Room Controls")]
        [SerializeField, Tooltip("방 나가기 버튼")]
        private Button _leaveRoomButton;

        [SerializeField, Tooltip("준비 버튼 (방장 외 3명용, 토글 동작)")]
        private Button _readyButton;

        [SerializeField, Tooltip("준비/준비 해제 텍스트 표시용")]
        private Text _readyButtonText;

        [SerializeField, Tooltip("시작 버튼 (방장 전용)")]
        private Button _startButton;

        #endregion

        #region Unity Lifecycle Methods

        private void OnEnable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated += RefreshRoomUI;
                RefreshRoomUI();
            }

            RegisterButtonListeners();
        }

        private void OnDisable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated -= RefreshRoomUI;
            }

            UnregisterButtonListeners();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 로컬 플레이어의 CurrentPartyId로 해당 PartyInfo를 SyncList에서 찾아 멤버/악기 표시를 갱신한다.
        /// 이미 다른 플레이어가 선택한 악기 버튼은 interactable = false로 설정한다.
        /// </summary>
        public void RefreshRoomUI()
        {
            if (_bridge == null)
            {
                return;
            }

            if (NetworkClient.localPlayer == null)
            {
                return;
            }

            if (!NetworkClient.localPlayer.TryGetComponent(out LobbyPlayer localPlayer))
            {
                return;
            }

            int currentPartyId = localPlayer.CurrentPartyId;
            if (currentPartyId == 0)
            {
                return;
            }

            // SyncList에서 해당 파티 찾기
            PartyInfo targetParty = default;
            bool found = false;
            int partyListCount = _bridge.PartyList.Count;

            for (int i = 0; i < partyListCount; i++)
            {
                if (_bridge.PartyList[i]._PartyId == currentPartyId)
                {
                    targetParty = _bridge.PartyList[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return;
            }

            // 멤버 슬롯 표시 갱신
            for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                uint slotNetId = targetParty.GetSlot(slot);
                InstrumentType slotInstrument = targetParty.GetInstrument(slot);

                if (_memberNameTexts != null && slot < _memberNameTexts.Length &&
                    _memberNameTexts[slot] != null)
                {
                    if (slotNetId != 0)
                    {
                        string displayName = slotNetId.ToString();
                        if (NetworkClient.spawned.TryGetValue(slotNetId, out NetworkIdentity identity) &&
                            identity != null &&
                            identity.TryGetComponent(out LobbyPlayer slotPlayer))
                        {
                            displayName = slotPlayer.UserName;
                        }

                        _memberNameTexts[slot].text = displayName;
                    }
                    else
                    {
                        _memberNameTexts[slot].text = string.Empty;
                    }
                }

                if (_memberInstrumentTexts != null && slot < _memberInstrumentTexts.Length &&
                    _memberInstrumentTexts[slot] != null)
                {
                    _memberInstrumentTexts[slot].text = slotNetId != 0 ? slotInstrument.ToString() : string.Empty;
                }
            }

            // 이미 다른 플레이어가 선택한 악기 버튼 비활성화
            bool drumTaken = IsInstrumentTakenByOther(targetParty, localPlayer.netId, InstrumentType.Drum);
            bool guitarTaken = IsInstrumentTakenByOther(targetParty, localPlayer.netId, InstrumentType.Guitar);
            bool bassTaken = IsInstrumentTakenByOther(targetParty, localPlayer.netId, InstrumentType.Bass);
            bool keyboardTaken = IsInstrumentTakenByOther(targetParty, localPlayer.netId, InstrumentType.Keyboard);

            if (_drumButton != null)
            {
                _drumButton.interactable = !drumTaken;
            }

            if (_guitarButton != null)
            {
                _guitarButton.interactable = !guitarTaken;
            }

            if (_bassButton != null)
            {
                _bassButton.interactable = !bassTaken;
            }

            if (_keyboardButton != null)
            {
                _keyboardButton.interactable = !keyboardTaken;
            }

            // 방장 여부에 따라 준비 버튼 / 시작 버튼 표시 전환
            bool isLeader = localPlayer.netId == targetParty._Slot0NetId;

            if (_readyButton != null)
            {
                _readyButton.gameObject.SetActive(!isLeader);
            }

            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(isLeader);
            }

            if (isLeader)
            {
                // 방장: 시작 버튼 활성화 조건 로컬 평가
                bool canStart = EvaluateCanStart(targetParty);
                if (_startButton != null)
                {
                    _startButton.interactable = canStart;
                }
            }
            else
            {
                // 팀원: 본인 준비 상태에 따라 버튼 텍스트 변경
                int localSlot = GetLocalSlot(targetParty, localPlayer.netId);
                bool isReady = localSlot > 0 && targetParty.GetReady(localSlot);

                if (_readyButtonText != null)
                {
                    _readyButtonText.text = isReady ? "준비 해제" : "준비";
                }
            }
        }

        #endregion

        #region Private Methods

        private void RegisterButtonListeners()
        {
            if (_drumButton != null)
            {
                _drumButton.onClick.RemoveAllListeners();
                _drumButton.onClick.AddListener(OnClickDrum);
            }

            if (_guitarButton != null)
            {
                _guitarButton.onClick.RemoveAllListeners();
                _guitarButton.onClick.AddListener(OnClickGuitar);
            }

            if (_bassButton != null)
            {
                _bassButton.onClick.RemoveAllListeners();
                _bassButton.onClick.AddListener(OnClickBass);
            }

            if (_keyboardButton != null)
            {
                _keyboardButton.onClick.RemoveAllListeners();
                _keyboardButton.onClick.AddListener(OnClickKeyboard);
            }

            if (_leaveRoomButton != null)
            {
                _leaveRoomButton.onClick.RemoveAllListeners();
                _leaveRoomButton.onClick.AddListener(OnClickLeaveRoom);
            }

            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveAllListeners();
                _readyButton.onClick.AddListener(OnClickReady);
            }

            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners();
                _startButton.onClick.AddListener(OnClickStart);
            }
        }

        private void UnregisterButtonListeners()
        {
            if (_drumButton != null)
            {
                _drumButton.onClick.RemoveAllListeners();
            }

            if (_guitarButton != null)
            {
                _guitarButton.onClick.RemoveAllListeners();
            }

            if (_bassButton != null)
            {
                _bassButton.onClick.RemoveAllListeners();
            }

            if (_keyboardButton != null)
            {
                _keyboardButton.onClick.RemoveAllListeners();
            }

            if (_leaveRoomButton != null)
            {
                _leaveRoomButton.onClick.RemoveAllListeners();
            }

            if (_readyButton != null)
            {
                _readyButton.onClick.RemoveAllListeners();
            }

            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners();
            }
        }

        private void OnClickDrum()
        {
            RequestInstrument(InstrumentType.Drum);
        }

        private void OnClickGuitar()
        {
            RequestInstrument(InstrumentType.Guitar);
        }

        private void OnClickBass()
        {
            RequestInstrument(InstrumentType.Bass);
        }

        private void OnClickKeyboard()
        {
            RequestInstrument(InstrumentType.Keyboard);
        }

        private void OnClickLeaveRoom()
        {
            if (_bridge != null)
            {
                _bridge.RequestLeaveParty();
            }

            if (_lobbyUIManager != null)
            {
                _lobbyUIManager.ShowPartyList();
            }
        }

        private void RequestInstrument(InstrumentType instrument)
        {
            if (_bridge == null)
            {
                return;
            }

            if (NetworkClient.localPlayer == null)
            {
                return;
            }

            if (!NetworkClient.localPlayer.TryGetComponent(out LobbyPlayer localPlayer))
            {
                return;
            }

            int partyId = localPlayer.CurrentPartyId;
            if (partyId == 0)
            {
                return;
            }

            _bridge.RequestSelectInstrument(partyId, instrument);
        }

        private void OnClickReady()
        {
            if (_bridge == null || NetworkClient.localPlayer == null)
            {
                return;
            }

            if (!NetworkClient.localPlayer.TryGetComponent(out LobbyPlayer localPlayer))
            {
                return;
            }

            int partyId = localPlayer.CurrentPartyId;
            if (partyId == 0)
            {
                return;
            }

            // 현재 준비 상태 조회 후 토글
            PartyInfo targetParty = default;
            bool found = false;
            int partyListCount = _bridge.PartyList.Count;

            for (int i = 0; i < partyListCount; i++)
            {
                if (_bridge.PartyList[i]._PartyId == partyId)
                {
                    targetParty = _bridge.PartyList[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return;
            }

            int localSlot = GetLocalSlot(targetParty, localPlayer.netId);
            bool currentReady = localSlot > 0 && targetParty.GetReady(localSlot);
            _bridge.RequestSetReady(!currentReady);
        }

        private void OnClickStart()
        {
            if (_bridge != null)
            {
                _bridge.RequestStartMatch();
            }
        }

        private bool EvaluateCanStart(PartyInfo party)
        {
            if (party._MemberCount != PartyConstants.MAX_PARTY_MEMBERS)
            {
                return false;
            }

            for (int slot = 1; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                if (!party.GetReady(slot))
                {
                    return false;
                }
            }

            for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                if (party.GetInstrument(slot) == InstrumentType.None)
                {
                    return false;
                }
            }

            return true;
        }

        private int GetLocalSlot(PartyInfo party, uint localNetId)
        {
            for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                if (party.GetSlot(slot) == localNetId)
                {
                    return slot;
                }
            }

            return -1;
        }

        private bool IsInstrumentTakenByOther(PartyInfo party, uint localNetId, InstrumentType instrument)
        {
            for (int slot = 0; slot < PartyConstants.MAX_PARTY_MEMBERS; slot++)
            {
                if (party.GetSlot(slot) == localNetId)
                {
                    continue;
                }

                if (party.GetSlot(slot) != 0 && party.GetInstrument(slot) == instrument)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
