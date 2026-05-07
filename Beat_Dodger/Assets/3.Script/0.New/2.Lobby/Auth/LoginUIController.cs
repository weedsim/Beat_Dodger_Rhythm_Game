using UnityEngine;
using UnityEngine.UI;
using BeatDodger.Network;
using TMPro;

namespace BeatDodger.UI
{
    /// <summary>
    /// 로그인 화면의 UI 요소들을 관리하고 사용자 입력을 처리하는 컨트롤러 (Legacy UI 버전)
    /// </summary>
    public class LoginUIController : MonoBehaviour
    {
        #region Variables

        [Header("Login UI References")]
        [SerializeField, Tooltip("Login UI 내 ID 입력칸")] private TMP_InputField _loginIdInputField;
        [SerializeField, Tooltip("Login UI 내 PW 입력칸")] private TMP_InputField _loginPasswordInputField;
        [SerializeField, Tooltip("Login Button")] private Button _loginButton;

        [Header("Register UI References")]
        [SerializeField, Tooltip("Register UI 내 ID 입력칸")] private TMP_InputField _registerIdInputField;
        [SerializeField, Tooltip("Register UI 내 PW 입력칸")] private TMP_InputField _registerPasswordInputField;
        [SerializeField, Tooltip("Register Button")] private Button _registerButton;

        [Header("Dependencies")]
        [SerializeField] private CustomNetworkManager _networkManager;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
#if !UNITY_SERVER
            //_loginButton.onClick.AddListener(OnLoginButtonClicked);
            //_registerButton.onClick.AddListener(OnLoginButtonClicked);
#endif
        }

#endregion

        #region Public Methods (Client Side)

#if !UNITY_SERVER
        /// <summary>
        /// 로그인 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnLoginButtonClicked()
        {
            string userId = _loginIdInputField != null ? _loginIdInputField.text : string.Empty;
            string password = _loginPasswordInputField != null ? _loginPasswordInputField.text : string.Empty;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[LoginUIController] ID 또는 비밀번호가 입력되지 않았습니다.");
                // TODO: 사용자에게 알림을 주는 Legacy UI 팝업 호출 필요
                return;
            }

            if (_networkManager != null)
            {
                Debug.Log($"[LoginUIController] Requesting login for: {userId}");
                _networkManager.AttemptLogin(userId, password);
            }
            else
            {
                Debug.LogError("[LoginUIController] CustomNetworkManager reference is missing!");
            }
        }

        /// <summary>
        /// 회원가입 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnRegisterButtonClicked()
        {
            string userId = _registerIdInputField != null ? _registerIdInputField.text : string.Empty;
            string password = _registerPasswordInputField != null ? _registerPasswordInputField.text : string.Empty;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[LoginUIController] ID 또는 비밀번호가 입력되지 않았습니다.");
                // TODO: 사용자에게 알림을 주는 Legacy UI 팝업 호출 필요
                return;
            }

            if (_networkManager != null)
            {
                Debug.Log("[LoginUIController] Requesting Register");
                _networkManager.AttemptLogin(userId, password);
            }
            else
            {
                Debug.LogError("[LoginUIController] CustomNetworkManager reference is missing!");
            }
        }
#endif

        #endregion
    }
}
