using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using BeatDodger.UI;

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

        [Header("UI Controls")]
        [SerializeField, Tooltip("파티 목록 GameObject들")] private List<GameObject> _partyUiParentObjects;
        [SerializeField, Tooltip("파티 생성 시 입력해야하는 파티 이름 InputField")] private InputField _roomNameInputField;
        [SerializeField, Tooltip("파티 생성 버튼")] private Button _createPartyButton;
        [SerializeField, Tooltip("캐싱된 파티 이름을 띄우는 UI들")] private Text[] _partyTextUiCache;
        [SerializeField, Tooltip("캐싱된 파티 참가 버튼들 (파티 목록 항목 클릭 시 RequestJoinParty 호출)")] private Button[] _partyButtonCache;

        [Header("Cached Data")]
        [SerializeField, Tooltip("캐싱된 최대 파티 수")] private int _maxParties;
        [SerializeField, Tooltip("캐싱된 파티 정보들")] private List<PartyInfo> _cachedPartyList = new List<PartyInfo>();

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            // S4: Inspector 주입 우선 사용. 없으면 싱글톤 fallback
            if (_bridge == null)
            {
                _bridge = PartyNetworkBridge.Instance;
            }

            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] PartyNetworkBridge 참조가 없습니다 Inspector에서 연결하거나 씬에 PartyNetworkBridge가 있어야 합니다");
                return;
            }

            _maxParties = _bridge.MaxParties;
            _partyTextUiCache = new Text[_maxParties];
            _partyButtonCache = new Button[_maxParties];

            for (int i = 0; i < _maxParties; i++)
            {
                if (i >= _partyUiParentObjects.Count || _partyUiParentObjects[i] == null)
                {
                    continue;
                }

                if (_partyUiParentObjects[i].transform.GetChild(0).TryGetComponent(out Text text))
                {
                    _partyTextUiCache[i] = text;
                }

                // 파티 항목 Button 캐싱 — 부모에 Button이 있으면 우선 사용, 없으면 자식에서 탐색
                if (_partyUiParentObjects[i].TryGetComponent(out Button button))
                {
                    _partyButtonCache[i] = button;
                }
                else
                {
                    _partyButtonCache[i] = _partyUiParentObjects[i].GetComponentInChildren<Button>(true);
                }
            }
        }

        private void OnEnable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated += HandlePartyListUpdated;

                // UI가 켜질 때 서버에서 이미 받아둔 초기 데이터를 즉시 1회 캐싱
                HandlePartyListUpdated();
            }

            LobbyPlayer.OnLocalPartyIdChanged += HandleLocalPartyIdChanged;

            if (_createPartyButton != null)
            {
                _createPartyButton.onClick.RemoveAllListeners();
                _createPartyButton.onClick.AddListener(OnClickCreatePartyButton);
            }
        }

        private void OnDisable()
        {
            if (_bridge != null)
            {
                _bridge.OnPartyListUpdated -= HandlePartyListUpdated;
            }

            LobbyPlayer.OnLocalPartyIdChanged -= HandleLocalPartyIdChanged;

            if (_createPartyButton != null)
            {
                _createPartyButton.onClick.RemoveAllListeners();
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

        #endregion

        #region Public Methods

        /// <summary>
        /// 파티 목록 서브패널을 표시하고 방 서브패널을 숨긴다.
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
        }

        /// <summary>
        /// 방 내부 서브패널을 표시하고 파티 목록 서브패널을 숨긴다.
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

            if (_roomUIController != null)
            {
                _roomUIController.RefreshRoomUI();
            }
        }

        /// <summary>
        /// 방 생성 버튼 클릭 시 호출된다. UI 입력값을 읽어 서버에 파티 생성을 요청한다.
        /// </summary>
        public void OnClickCreatePartyButton()
        {
            if (_roomNameInputField == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] 방 이름 입력 칸(RoomNameInputField)은 필수적으로 연결시켜주어야 합니다.");
                return;
            }

            string roomNameStr = _roomNameInputField.text;

            Debug.Log($"[LobbyUIManager] [Client] 생성할 방의 이름은 {roomNameStr}");

            if (string.IsNullOrWhiteSpace(roomNameStr))
            {
                // TODO: 이름 입력이 필수라는 로그를 띄우기
                Debug.Log("[LobbyUIManager] [Client] 방을 생성하기 위해서는 이름을 지정해야 합니다.");
                return;
            }

            // TODO: 난이도 설정 UI가 추가되면 해당 컴포넌트의 값을 읽어오도록 수정
            int difficultyInt = 1;

            Debug.Log($"[LobbyUIManager] [Client] 생성시킬 방의 정보 | RoomName: {roomNameStr} | Difficulty: {difficultyInt}");

            // K8: "같습니다/다릅니다" 디버그 로그 제거 — null 체크 후 바로 요청
            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] PartyNetworkBridge 참조가 없어 파티 생성을 요청할 수 없습니다.");
                return;
            }

            _bridge.RequestCreateParty(roomNameStr, difficultyInt);
        }

        #endregion

        #region Private Methods

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

        private void HandlePartyListUpdated()
        {
            if (_bridge == null)
            {
                return;
            }

            _bridge.GetActiveParties(_cachedPartyList);
            DrawUi();

            // 로컬 플레이어의 CurrentPartyId에 따라 서브패널 자동 전환
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
                    if (_partyUiParentObjects[i] != null)
                    {
                        _partyUiParentObjects[i].SetActive(true);
                    }

                    if (_partyTextUiCache[i] != null)
                    {
                        _partyTextUiCache[i].text = _cachedPartyList[i]._RoomName;
                    }

                    if (_partyButtonCache[i] != null)
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

        private void OnClickJoinPartyButton(int partyId)
        {
            if (_bridge == null)
            {
                Debug.LogError("[LobbyUIManager] [Client] PartyNetworkBridge 참조가 없어 파티 참가를 요청할 수 없습니다.");
                return;
            }

            _bridge.RequestJoinParty(partyId);
        }

        #endregion
    }
}
