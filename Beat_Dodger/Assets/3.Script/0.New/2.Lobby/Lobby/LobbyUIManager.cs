using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using BeatDodger.UI;
using TMPro;

namespace BeatDodger.Lobby
{
    /// <summary>
    /// 로비 UI를 관리하는 컨트롤러.
    /// PartyNetworkBridge의 이벤트를 구독하여 파티 목록 UI를 갱신한다.
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        #region Variables

        [Header("Network Bridge")]
        [SerializeField, Tooltip("파티 네트워크 브릿지 (Inspector에서 직접 연결)")]
        private PartyNetworkBridge _bridge;

        [Header("Sub Panels")]
        [SerializeField, Tooltip("파티 목록 서브패널 (방 목록 UI 전체)")]
        private GameObject _partyListSubPanel;
        [SerializeField, Tooltip("방 내부 서브패널 (악기 선택 및 멤버 목록 UI)")]
        private GameObject _roomSubPanel;
        [SerializeField, Tooltip("방 내부 UI 컨트롤러")]
        private RoomUIController _roomUIController;

        [Header("Create Room Panel")]
        [SerializeField, Tooltip("방 생성 정보 입력 패널 (방 만들기 버튼 클릭 시 표시)")]
        private GameObject _createRoomPanel;
        [SerializeField, Tooltip("방 생성 패널 — 방 이름 입력 InputField")]
        private TMP_InputField _createRoomNameInputField;
        [SerializeField, Tooltip("방 생성 패널 — 비밀번호 입력 InputField (빈 값이면 공개방)")]
        private TMP_InputField _createRoomPasswordInputField;
        [SerializeField, Tooltip("방 생성 패널 — 생성 확인 버튼")]
        private Button _confirmCreateButton;
        [SerializeField, Tooltip("방 생성 패널 — 취소 버튼")]
        private Button _cancelCreateButton;

        [Header("Password Join Panel")]
        [SerializeField, Tooltip("비밀번호 입력 패널 (비공개 방 입장 시 표시)")]
        private GameObject _passwordJoinPanel;
        [SerializeField, Tooltip("비밀번호 입력 패널 — 비밀번호 InputField")]
        private TMP_InputField _joinPasswordInputField;
        [SerializeField, Tooltip("비밀번호 입력 패널 — 클릭한 방 이름을 표시하는 Text")]
        private TMP_Text _joinRoomNameText;
        [SerializeField, Tooltip("비밀번호 입력 로그 - 비밀번호 입력에 대한 Log Text")]
        private TMP_Text _passwordLog;
        [SerializeField, Tooltip("비밀번호 입력 패널 — 입장 확인 버튼")]
        private Button _confirmJoinButton;
        [SerializeField, Tooltip("비밀번호 입력 패널 — 취소 버튼")]
        private Button _cancelJoinButton;

        [Header("Party List UI")]
        [SerializeField, Tooltip("파티 목록 GameObject들")]
        private List<GameObject> _partyUiParentObjects;
        [SerializeField, Tooltip("방 만들기 버튼 (클릭 시 방 생성 패널 표시)")]
        private Button _openCreatePanelButton;
        [SerializeField, Tooltip("랜덤 매칭 버튼 (비밀번호 없는 방 중 입장 가능한 곳 자동 입장)")]
        private Button _randomMatchButton;
        [SerializeField, Tooltip("파티 목록 수동 새로고침 버튼")]
        private Button _refreshButton;
        [SerializeField, Range(0f, 60f), Tooltip("새로고침 버튼 재사용 대기 시간 (초)")]
        private float _refreshCooldown = 5f;
        [SerializeField, Tooltip("방 목록 항목별 — 곡 이름 Text (항목 수만큼)")]
        private TMP_Text[] _partySongNameTextCache;
        [SerializeField, Tooltip("방 목록 항목별 — 방 이름 Text (항목 수만큼)")]
        private TMP_Text[] _partyRoomNameTextCache;
        [SerializeField, Tooltip("방 목록 항목별 — 비밀번호 유무 Image (항목 수만큼)")]
        private Image[] _partyPasswordImageCache;
        [SerializeField, Tooltip("방 목록 항목별 — 현재/최대 인원 Text e.g. \"1/4\" (항목 수만큼)")]
        private TMP_Text[] _partyMemberCountTextCache;
        [SerializeField, Tooltip("캐싱된 파티 참가 버튼들 (클릭 시 비밀번호 확인 후 RequestJoinParty 호출)")]
        private Button[] _partyButtonCache;

        [Header("Password Image Sprites")]
        [SerializeField, Tooltip("비밀번호 있는 방 자물쇠 이미지")]
        private Sprite _passwordSprite;
        [SerializeField, Tooltip("비밀번호 없는 방 이미지")]
        private Sprite _noPasswordSprite;

        [Header("Cached Data")]
        [SerializeField, Tooltip("캐싱된 최대 파티 수")]
        private int _maxParties;
        [SerializeField, Tooltip("캐싱된 파티 정보들")]
        private List<PartyInfo> _cachedPartyList = new List<PartyInfo>();

        private int _pendingJoinPartyId = 0;
        private float _nextRefreshAllowedTime = 0f;

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            if (_bridge == null)
            {
                _bridge = PartyNetworkBridge.Instance;
            }

            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] PartyNetworkBridge 참조가 없습니다. Inspector에서 연결하거나 씬에 PartyNetworkBridge가 있어야 합니다.");
                return;
            }

            _maxParties = _bridge.MaxParties;
            _partyButtonCache = new Button[_maxParties];

            for (int i = 0; i < _maxParties; i++)
            {
                if (i >= _partyUiParentObjects.Count || _partyUiParentObjects[i] == null)
                {
                    continue;
                }

                if (_partyUiParentObjects[i].TryGetComponent(out Button button))
                {
                    _partyButtonCache[i] = button;
                }
                else
                {
                    _partyButtonCache[i] = _partyUiParentObjects[i].GetComponentInChildren<Button>(true);
                }
            }

            if (_createRoomPanel != null)
            {
                _createRoomPanel.SetActive(false);
            }

            if (_passwordJoinPanel != null)
            {
                _passwordJoinPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated += HandlePartyListUpdated;
                _bridge.OnJoinFailed += HandleJoinFailed;
                RefreshPartyList();
            }

            LobbyPlayer.OnLocalPartyIdChanged += HandleLocalPartyIdChanged;

            RegisterButtonListeners();
        }

        private void Update()
        {
            if (_refreshButton != null)
            {
                _refreshButton.interactable = Time.time >= _nextRefreshAllowedTime;
            }
        }

        private void OnDisable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated -= HandlePartyListUpdated;
                _bridge.OnJoinFailed -= HandleJoinFailed;
            }

            LobbyPlayer.OnLocalPartyIdChanged -= HandleLocalPartyIdChanged;

            UnregisterButtonListeners();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 파티 목록 서브패널을 표시하고 방 서브패널을 숨긴다.
        /// 열려 있던 비밀번호 입력 패널도 함께 닫는다.
        /// </summary>
        public void ShowPartyList()
        {
            if (_partyListSubPanel != null)
            {
                _partyListSubPanel.SetActive(true);
            }

            if (_roomSubPanel != null)
            {
                _roomSubPanel.SetActive(false);
            }

            CancelPendingJoin();
        }

        /// <summary>
        /// 방 내부 서브패널을 표시하고 파티 목록 서브패널을 숨긴다.
        /// 열려 있던 비밀번호 입력 패널도 함께 닫는다.
        /// </summary>
        public void ShowRoom()
        {
            if (_partyListSubPanel != null)
            {
                _partyListSubPanel.SetActive(false);
            }

            if (_roomSubPanel != null)
            {
                _roomSubPanel.SetActive(true);
            }

            CancelPendingJoin();

            if (_roomUIController != null)
            {
                _roomUIController.RefreshRoomUI();
            }
        }

        #endregion

        #region Private Methods

        private void CancelPendingJoin()
        {
            _pendingJoinPartyId = 0;

            if (_joinRoomNameText != null)
            {
                _joinRoomNameText.text = string.Empty;
            }

            if (_passwordLog != null)
            {
                _passwordLog.text = string.Empty;
            }

            if (_passwordJoinPanel != null)
            {
                _passwordJoinPanel.SetActive(false);
            }
        }

        private void RegisterButtonListeners()
        {
            if (_openCreatePanelButton != null)
            {
                _openCreatePanelButton.onClick.RemoveAllListeners();
                _openCreatePanelButton.onClick.AddListener(OnClickOpenCreatePanel);
            }

            if (_randomMatchButton != null)
            {
                _randomMatchButton.onClick.RemoveAllListeners();
                _randomMatchButton.onClick.AddListener(OnClickRandomMatch);
            }

            if (_confirmCreateButton != null)
            {
                _confirmCreateButton.onClick.RemoveAllListeners();
                _confirmCreateButton.onClick.AddListener(OnClickConfirmCreate);
            }

            if (_cancelCreateButton != null)
            {
                _cancelCreateButton.onClick.RemoveAllListeners();
                _cancelCreateButton.onClick.AddListener(OnClickCancelCreate);
            }

            if (_confirmJoinButton != null)
            {
                _confirmJoinButton.onClick.RemoveAllListeners();
                _confirmJoinButton.onClick.AddListener(OnClickConfirmJoin);
            }

            if (_cancelJoinButton != null)
            {
                _cancelJoinButton.onClick.RemoveAllListeners();
                _cancelJoinButton.onClick.AddListener(OnClickCancelJoin);
            }

            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveAllListeners();
                _refreshButton.onClick.AddListener(OnClickRefresh);
            }
        }

        private void UnregisterButtonListeners()
        {
            if (_openCreatePanelButton != null)
            {
                _openCreatePanelButton.onClick.RemoveAllListeners();
            }

            if (_randomMatchButton != null)
            {
                _randomMatchButton.onClick.RemoveAllListeners();
            }

            if (_confirmCreateButton != null)
            {
                _confirmCreateButton.onClick.RemoveAllListeners();
            }

            if (_cancelCreateButton != null)
            {
                _cancelCreateButton.onClick.RemoveAllListeners();
            }

            if (_confirmJoinButton != null)
            {
                _confirmJoinButton.onClick.RemoveAllListeners();
            }

            if (_cancelJoinButton != null)
            {
                _cancelJoinButton.onClick.RemoveAllListeners();
            }

            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveAllListeners();
            }

            if (_partyButtonCache != null)
            {
                for (int i = 0; i < _partyButtonCache.Length; i++)
                {
                    if (_partyButtonCache[i] != null)
                    {
                        _partyButtonCache[i].onClick.RemoveAllListeners();
                    }
                }
            }
        }

        private void HandleLocalPartyIdChanged(int newPartyId)
        {
            if (newPartyId != 0)
            {
                ShowRoom();
            }
            else
            {
                ShowPartyList();
            }
        }

        private void RefreshPartyList()
        {
            if (_bridge == null)
            {
                return;
            }

            _bridge.GetActiveParties(_cachedPartyList);
            DrawUi();
        }

        private void OnClickRefresh()
        {
            if (Time.time < _nextRefreshAllowedTime)
            {
                return;
            }

            _nextRefreshAllowedTime = Time.time + _refreshCooldown;
            RefreshPartyList();
        }

        private void HandlePartyListUpdated()
        {
            if (_bridge == null)
            {
                return;
            }

            // 상시 자동 갱신 제거: 패널 전환만 처리한다 (목록 갱신은 초기 로드·수동 새로고침에서만)
            if (NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.TryGetComponent(out LobbyPlayer localPlayer))
            {
                bool isInParty = localPlayer.CurrentPartyId != 0;

                if (isInParty && _partyListSubPanel != null && _partyListSubPanel.activeSelf)
                {
                    ShowRoom();
                }
                else if (!isInParty && _roomSubPanel != null && _roomSubPanel.activeSelf)
                {
                    ShowPartyList();
                }
            }
        }

        private void DrawUi()
        {
            for (int i = 0; i < _maxParties; i++)
            {
                if (i < _cachedPartyList.Count)
                {
                    if (i < _partyUiParentObjects.Count && _partyUiParentObjects[i] != null)
                    {
                        _partyUiParentObjects[i].SetActive(true);
                    }

                    if (i < _partySongNameTextCache.Length && _partySongNameTextCache[i] != null)
                    {
                        _partySongNameTextCache[i].text = _cachedPartyList[i]._SongName;
                        // [FALLBACK] 곡 이름 실시간 갱신으로 서버 부하가 심할 경우 위 줄을 주석 처리하고 아래 줄을 활성화한다.
                        // _partySongNameTextCache[i].text = $"#{_cachedPartyList[i]._PartyId}";
                    }

                    if (i < _partyRoomNameTextCache.Length && _partyRoomNameTextCache[i] != null)
                    {
                        _partyRoomNameTextCache[i].text = _cachedPartyList[i]._RoomName;
                    }

                    if (i < _partyPasswordImageCache.Length && _partyPasswordImageCache[i] != null)
                    {
                        bool hasPassword = _cachedPartyList[i]._HasPassword;
                        Sprite target = hasPassword ? _passwordSprite : _noPasswordSprite;
                        if (target != null)
                        {
                            _partyPasswordImageCache[i].sprite = target;
                        }
                    }

                    if (i < _partyMemberCountTextCache.Length && _partyMemberCountTextCache[i] != null)
                    {
                        _partyMemberCountTextCache[i].text =
                            $"{_cachedPartyList[i]._MemberCount}/{_cachedPartyList[i]._MaxMembers}";
                    }

                    if (i < _partyButtonCache.Length && _partyButtonCache[i] != null)
                    {
                        int partyId = _cachedPartyList[i]._PartyId;
                        _partyButtonCache[i].onClick.RemoveAllListeners();
                        _partyButtonCache[i].onClick.AddListener(() => OnClickJoinPartyButton(partyId));
                    }
                }
                else
                {
                    if (i < _partyUiParentObjects.Count && _partyUiParentObjects[i] != null)
                    {
                        _partyUiParentObjects[i].SetActive(false);
                    }

                    if (i < _partyButtonCache.Length && _partyButtonCache[i] != null)
                    {
                        _partyButtonCache[i].onClick.RemoveAllListeners();
                    }
                }
            }
        }

        private void OnClickOpenCreatePanel()
        {
            if (_createRoomNameInputField != null)
            {
                _createRoomNameInputField.text = string.Empty;
            }

            if (_createRoomPasswordInputField != null)
            {
                _createRoomPasswordInputField.text = string.Empty;
            }

            if (_createRoomPanel != null)
            {
                _createRoomPanel.SetActive(true);
            }
        }

        private void OnClickConfirmCreate()
        {
            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] PartyNetworkBridge 참조가 없어 파티 생성을 요청할 수 없습니다.");
                return;
            }

            string roomName = _createRoomNameInputField != null ? _createRoomNameInputField.text : string.Empty;

            if (string.IsNullOrWhiteSpace(roomName))
            {
                Debug.Log("[LobbyUIManager] [Client] 방을 생성하기 위해서는 이름을 지정해야 합니다.");
                return;
            }

            string password = _createRoomPasswordInputField != null ? _createRoomPasswordInputField.text : string.Empty;

            Debug.Log($"[LobbyUIManager] [Client] 방 생성 요청 | 이름: {roomName} | 비밀번호 설정: {!string.IsNullOrEmpty(password)}");

            // 곡 선택은 대기실(RoomUIController)에서 방장이 선택하므로 생성 시 기본값(0, 빈 문자열)으로 전송한다.
            _bridge.RequestCreateParty(roomName, password, 0, string.Empty);

            if (_createRoomPanel != null)
            {
                _createRoomPanel.SetActive(false);
            }
        }

        private void OnClickCancelCreate()
        {
            if (_createRoomPanel != null)
            {
                _createRoomPanel.SetActive(false);
            }
        }

        private void OnClickRandomMatch()
        {
            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] PartyNetworkBridge 참조가 없어 랜덤 매칭을 요청할 수 없습니다.");
                return;
            }

            for (int i = 0; i < _cachedPartyList.Count; i++)
            {
                PartyInfo party = _cachedPartyList[i];
                if (!party._HasPassword && party._MemberCount < party._MaxMembers)
                {
                    Debug.Log($"[LobbyUIManager] [Client] 랜덤 매칭 — 공개 방 입장 | PartyId: {party._PartyId} | 방 이름: {party._RoomName}");
                    _bridge.RequestJoinParty(party._PartyId, string.Empty);
                    return;
                }
            }

            Debug.Log("[LobbyUIManager] [Client] 랜덤 매칭 — 입장 가능한 공개 방이 없습니다.");
        }

        private void OnClickJoinPartyButton(int partyId)
        {
            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] PartyNetworkBridge 참조가 없어 파티 참가를 요청할 수 없습니다.");
                return;
            }

            PartyInfo targetParty = default;
            bool found = false;

            for (int i = 0; i < _cachedPartyList.Count; i++)
            {
                if (_cachedPartyList[i]._PartyId == partyId)
                {
                    targetParty = _cachedPartyList[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return;
            }

            if (targetParty._HasPassword)
            {
                _pendingJoinPartyId = partyId;

                if (_joinRoomNameText != null)
                {
                    _joinRoomNameText.text = targetParty._RoomName;
                }

                if (_passwordLog != null)
                {
                    _passwordLog.text = string.Empty;
                }

                if (_joinPasswordInputField != null)
                {
                    _joinPasswordInputField.text = string.Empty;
                }

                if (_passwordJoinPanel != null)
                {
                    _passwordJoinPanel.SetActive(true);
                }
            }
            else
            {
                _bridge.RequestJoinParty(partyId, string.Empty);
            }
        }

        private void OnClickConfirmJoin()
        {
            if (_pendingJoinPartyId == 0)
            {
                return;
            }

            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] PartyNetworkBridge 참조가 없어 파티 참가를 요청할 수 없습니다.");
                return;
            }

            string password = _joinPasswordInputField != null ? _joinPasswordInputField.text : string.Empty;

            Debug.Log($"[LobbyUIManager] [Client] 비밀번호 입장 요청 | PartyId: {_pendingJoinPartyId}");

            _bridge.RequestJoinParty(_pendingJoinPartyId, password);
            // 패널 닫기는 서버 응답에 따라 처리한다.
            // 성공 → HandleLocalPartyIdChanged(newId != 0) → ShowRoom() → CancelPendingJoin()
            // 실패 → HandleJoinFailed() → 오류 메시지 표시, 패널 유지
        }

        private void OnClickCancelJoin()
        {
            CancelPendingJoin();
        }

        private void HandleJoinFailed()
        {
            if (_passwordLog != null)
            {
                _passwordLog.text = "비밀번호가 일치하지 않습니다";
            }
        }

        #endregion
    }
}
