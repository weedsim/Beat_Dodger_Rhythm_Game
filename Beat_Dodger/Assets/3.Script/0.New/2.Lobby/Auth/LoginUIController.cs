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

        [Header("UI References")]
        [SerializeField] private TMP_InputField _idInputField;
        [SerializeField] private TMP_InputField _passwordInputField;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Button _goToRegisterButton;

        [Header("Dependencies")]
        [SerializeField] private CustomNetworkManager _networkManager;
        private ILoginEntryPoint _loginEntryPoint;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _loginEntryPoint = _networkManager;
        }

        #endregion

#region Public Methods (Client Side)

#if !UNITY_SERVER
        /// <summary>
        /// 로그인 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnLoginButtonClicked()
        {
            string userId   = _idInputField       != null ? _idInputField.text       : string.Empty;
            string password = _passwordInputField != null ? _passwordInputField.text : string.Empty;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[LoginUI] ID 또는 비밀번호가 입력되지 않았습니다.");
                // TODO: 사용자에게 알림을 주는 Legacy UI 팝업 호출 필요
                return;
            }

            if (_loginEntryPoint != null)
            {
                Debug.Log($"[LoginUI] Requesting login for: {userId}");
                _loginEntryPoint.AttemptLogin(userId, password);
                ClearInputFields();
            }
            else
            {
                Debug.LogError("[LoginUI] ILoginEntryPoint reference is missing!");
            }
        }

        /// <summary>
        /// 회원가입 화면으로 이동 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnGoToRegisterButtonClicked()
        {
            UIManager.Instance?.TransitionToRegister();
            ClearInputFields();
        }

        private void ClearInputFields()
        {
            if (_idInputField != null)       _idInputField.text       = string.Empty;
            if (_passwordInputField != null) _passwordInputField.text = string.Empty;
        }
#endif

#endregion
    }
}
