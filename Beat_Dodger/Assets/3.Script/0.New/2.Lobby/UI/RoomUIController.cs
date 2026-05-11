using System.Collections.Generic;
using BeatDodger.Core;
using BeatDodger.Lobby;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        private TextMeshProUGUI[] _memberNameTexts;

        [SerializeField, Tooltip("멤버 악기 이미지 (4개, Slot0~3 순서)")]
        private Image[] _memberInstrumentImages;

        [Header("Instrument Sprites")]
        [SerializeField, Tooltip("드럼 악기 스프라이트")]
        private Sprite _drumSprite;

        [SerializeField, Tooltip("기타 악기 스프라이트")]
        private Sprite _guitarSprite;

        [SerializeField, Tooltip("베이스 악기 스프라이트")]
        private Sprite _bassSprite;

        [SerializeField, Tooltip("키보드 악기 스프라이트")]
        private Sprite _keyboardSprite;

        [Header("Instrument Buttons")]
        [SerializeField, Tooltip("드럼 선택 버튼")]
        private Button _drumButton;

        [SerializeField, Tooltip("기타 선택 버튼")]
        private Button _guitarButton;

        [SerializeField, Tooltip("베이스 선택 버튼")]
        private Button _bassButton;

        [SerializeField, Tooltip("키보드 선택 버튼")]
        private Button _keyboardButton;

        [Header("Song Info Display (All Clients)")]
        [SerializeField, Tooltip("선택된 곡 이름 텍스트 — 방 안 전체 클라이언트에 표시")]
        private TextMeshProUGUI _songInfoNameText;

        [SerializeField, Tooltip("선택된 곡 난이도 텍스트 — 방 안 전체 클라이언트에 표시")]
        private TextMeshProUGUI _songInfoDifficultyText;

        [SerializeField, Tooltip("선택된 곡 해시태그 분위기 텍스트 — 방 안 전체 클라이언트에 표시")]
        private TextMeshProUGUI _songInfoTagsText;

        [SerializeField, Tooltip("선택된 곡 썸네일 이미지 — 방 안 전체 클라이언트에 표시. SongId로 로컬 조회")]
        private Image _songInfoThumbnailImage;

        [Header("Song Selection (Leader Only)")]
        [SerializeField, Tooltip("곡 선택 패널 루트 GameObject — 방장일 때만 활성화된다")]
        private GameObject _songSelectPanel;

        [SerializeField, Tooltip("Scroll View의 Content RectTransform — 곡 버튼이 동적으로 생성된다")]
        private Transform _songScrollContent;

        [SerializeField, Tooltip("곡 버튼 프리팹 (Button + Text 구성)")]
        private GameObject _songButtonPrefab;

        [SerializeField, Tooltip("선택된 곡 버튼 색상 (회색 계열 권장)")]
        private Color _selectedSongColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [SerializeField, Tooltip("선택 해제된 곡 버튼 기본 색상")]
        private Color _normalSongColor = Color.white;

        [SerializeField, Tooltip("드롭다운에 표시할 곡 목록 (Inspector에서 등록)")]
        private List<SongData> _songList;

        private const float SONG_CONFIRM_DELAY = 5f;
        private float _songChangeTimer;
        private bool _pendingSongUpdate;
        private int _pendingSongIndex = -1;

        private readonly List<Button> _songButtons = new List<Button>();
        private int _selectedSongIndex = -1;
        private bool _defaultSongSent = false;

        [Header("Room Controls")]
        [SerializeField, Tooltip("방 나가기 버튼")]
        private Button _leaveRoomButton;

        [SerializeField, Tooltip("시작 버튼 (방장 전용 수동 백업용 — 악기 전원 선택 시 자동 시작됨)")]
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
            BuildSongScrollView();
        }

        private void OnDisable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated -= RefreshRoomUI;
            }

            UnregisterButtonListeners();
            _pendingSongUpdate = false;
            _defaultSongSent = false;
        }

        private void Update()
        {
            if (!_pendingSongUpdate)
            {
                return;
            }

            _songChangeTimer -= Time.deltaTime;
            if (_songChangeTimer <= 0f)
            {
                _pendingSongUpdate = false;
                CommitSongSelection();
            }
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

                if (_memberInstrumentImages != null && slot < _memberInstrumentImages.Length &&
                    _memberInstrumentImages[slot] != null)
                {
                    _memberInstrumentImages[slot].sprite = slotNetId != 0 ? GetInstrumentSprite(slotInstrument) : null;
                    _memberInstrumentImages[slot].enabled = slotNetId != 0 && _memberInstrumentImages[slot].sprite != null;
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

            // 방장만 시작 버튼 표시 (악기 선택이 곧 준비 — 준비 버튼은 사용하지 않음)
            bool isLeader = localPlayer.netId == targetParty._Slot0NetId;

            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(isLeader);

                if (isLeader)
                {
                    _startButton.interactable = EvaluateCanStart(targetParty);
                }
            }

            // 곡 선택 패널은 방장만 표시
            if (_songSelectPanel != null)
            {
                _songSelectPanel.SetActive(isLeader);
            }

            // pending 중이 아닐 때만 현재 파티의 곡 선택 상태와 버튼 UI를 동기화
            if (isLeader && _songList != null && !_pendingSongUpdate)
            {
                int matchIndex = 0;
                for (int i = 0; i < _songList.Count; i++)
                {
                    if (_songList[i]._Id == targetParty._SongId)
                    {
                        matchIndex = i;
                        break;
                    }
                }
                SelectSongVisual(matchIndex);
            }

            // 방장이 방을 생성한 직후 (SongId == 0)이면 첫 번째 곡을 기본값으로 서버에 즉시 전송한다
            // RefreshRoomUI는 SyncList 갱신 후 호출되므로 파티 데이터가 보장된다
            if (isLeader && targetParty._SongId == 0 && !_defaultSongSent &&
                _songList != null && _songList.Count > 0)
            {
                _defaultSongSent = true;
                SongData defaultSong = _songList[0];
                Debug.Log($"[RoomUIController] [Client] 기본 곡 자동 선택 전송 | PartyId: {currentPartyId} | 곡: {defaultSong._Name}");
                _bridge.RequestUpdateSong(currentPartyId, defaultSong._Id, defaultSong._Name, defaultSong._Difficulty, defaultSong._Tags ?? string.Empty);
            }

            // 곡 정보 패널 갱신 — 방 안 전체 클라이언트에 표시
            RefreshSongInfoDisplay(targetParty);
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

        private void BuildSongScrollView()
        {
            if (_songScrollContent == null || _songButtonPrefab == null || _songList == null)
            {
                return;
            }

            if (_songButtons.Count > 0)
            {
                return;
            }

            for (int i = 0; i < _songList.Count; i++)
            {
                int capturedIndex = i;
                GameObject go = Instantiate(_songButtonPrefab, _songScrollContent);
                Button btn = go.GetComponent<Button>();

                TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = _songList[i]._Name;
                }

                // 버튼 배경 Image(루트)를 제외한 자식 Image에 썸네일 적용
                Image[] childImages = go.GetComponentsInChildren<Image>();
                foreach (Image img in childImages)
                {
                    if (img.gameObject != go)
                    {
                        img.sprite = _songList[i]._Thumbnail;
                        img.enabled = _songList[i]._Thumbnail != null;
                        break;
                    }
                }

                if (btn != null)
                {
                    btn.onClick.AddListener(() => OnSongButtonClicked(capturedIndex));
                    _songButtons.Add(btn);
                }
            }

            // 첫 번째 곡을 기본 선택 상태로 표시 (서버 전송 없이 시각만 반영)
            if (_songList.Count > 0)
            {
                SelectSongVisual(0);
                _pendingSongIndex = 0;
            }
        }

        private void OnSongButtonClicked(int index)
        {
            // 이미 선택된 곡을 다시 눌러도 선택 해제되지 않고 유지
            if (index == _selectedSongIndex)
            {
                return;
            }

            SelectSongVisual(index);
            _pendingSongIndex = index;
            _pendingSongUpdate = true;
            _songChangeTimer = SONG_CONFIRM_DELAY;
        }

        private void SelectSongVisual(int index)
        {
            // 이전 선택 버튼 색상 해제
            if (_selectedSongIndex >= 0 && _selectedSongIndex < _songButtons.Count &&
                _songButtons[_selectedSongIndex] != null)
            {
                Image prevImg = _songButtons[_selectedSongIndex].GetComponent<Image>();
                if (prevImg != null)
                {
                    prevImg.color = _normalSongColor;
                }
            }

            _selectedSongIndex = index;

            // 새 선택 버튼 색상 적용
            if (index >= 0 && index < _songButtons.Count && _songButtons[index] != null)
            {
                Image img = _songButtons[index].GetComponent<Image>();
                if (img != null)
                {
                    img.color = _selectedSongColor;
                }
            }
        }

        private void CommitSongSelection()
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

            if (_songList == null || _pendingSongIndex < 0 || _pendingSongIndex >= _songList.Count)
            {
                return;
            }

            SongData selected = _songList[_pendingSongIndex];
            Debug.Log($"[RoomUIController] [Client] 곡 선택 확정 전송 | PartyId: {partyId} | 곡 ID: {selected._Id} | 곡: {selected._Name} | 난이도: {selected._Difficulty}");
            _bridge.RequestUpdateSong(partyId, selected._Id, selected._Name, selected._Difficulty, selected._Tags ?? string.Empty);
        }

        private void RefreshSongInfoDisplay(PartyInfo party)
        {
            if (_songInfoNameText != null)
            {
                _songInfoNameText.text = party._SongName;
            }

            if (_songInfoDifficultyText != null)
            {
                _songInfoDifficultyText.text = party._Difficulty.ToString();
            }

            if (_songInfoTagsText != null)
            {
                _songInfoTagsText.text = party._SongTags;
            }

            if (_songInfoThumbnailImage != null)
            {
                Sprite thumb = GetSongThumbnail(party._SongId);
                _songInfoThumbnailImage.sprite = thumb;
                _songInfoThumbnailImage.enabled = thumb != null;
            }
        }

        private Sprite GetSongThumbnail(int songId)
        {
            if (_songList == null)
            {
                return null;
            }

            for (int i = 0; i < _songList.Count; i++)
            {
                if (_songList[i]._Id == songId)
                {
                    return _songList[i]._Thumbnail;
                }
            }

            return null;
        }

        private Sprite GetInstrumentSprite(InstrumentType instrument)
        {
            return instrument switch
            {
                InstrumentType.Drum     => _drumSprite,
                InstrumentType.Guitar   => _guitarSprite,
                InstrumentType.Bass     => _bassSprite,
                InstrumentType.Keyboard => _keyboardSprite,
                _                       => null
            };
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
