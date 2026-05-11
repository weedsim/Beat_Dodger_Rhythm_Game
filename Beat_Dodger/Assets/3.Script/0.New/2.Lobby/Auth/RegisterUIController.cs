using UnityEngine;
using UnityEngine.UI;
using BeatDodger.Network;
using TMPro;

namespace BeatDodger.UI
{
    /// <summary>
    /// 회원가입 화면의 UI 요소들을 관리하고 사용자 입력을 처리하는 컨트롤러 (Legacy UI 버전)
    /// </summary>
    public class RegisterUIController : MonoBehaviour
    {
        #region Variables

        [Header("UI References")]
        [SerializeField, Tooltip("회원 가입 UI 내 ID 입력")] private TMP_InputField _idInputField;
        [SerializeField, Tooltip("회원 가입 UI 내 PW 입력")] private TMP_InputField _passwordInputField;
        [SerializeField, Tooltip("회원 가입 버튼")] private Button _registerButton;
        [SerializeField, Tooltip("회원 가입 패널에서 로그인 패널로 넘어가는 버튼")] private Button _backToLoginButton;

        [Header("Dependencies")]
        [SerializeField] private CustomNetworkManager _networkManager;
        private ILoginEntryPoint _loginEntryPoint;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if(_networkManager != null)
            {
                _loginEntryPoint = _networkManager;
            }
        }

        #endregion

#region Public Methods (Client Side)

#if !UNITY_SERVER
        /// <summary>
        /// 회원가입 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnRegisterButtonClicked()
        {
            string userId   = _idInputField       != null ? _idInputField.text       : string.Empty;
            string password = _passwordInputField != null ? _passwordInputField.text : string.Empty;
            string tempNickname = "User_" + userId;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(tempNickname))
            {
                Debug.LogWarning("[RegisterUI] 아이디, 비밀번호를 모두 입력해주세요.");
                // TODO: 사용자에게 알림을 주는 Legacy UI 팝업 호출 필요
                return;
            }

            if (_loginEntryPoint != null)
            {
                Debug.Log($"[RegisterUI] 회원가입 요청: {userId}");
                _loginEntryPoint.AttemptRegister(userId, password, tempNickname);
                ClearInputFields();
            }
            else
            {
                Debug.LogError("[RegisterUI] ILoginEntryPoint reference is missing!");
            }
        }

        /// <summary>
        /// 로그인 화면으로 돌아가기 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnBackToLoginButtonClicked()
        {
            UIManager.Instance?.TransitionToLogin();
            ClearInputFields();
        }
#endif

#endregion

        #region Private Methods

        private void ClearInputFields()
        {
            if (_idInputField != null)       _idInputField.text       = string.Empty;
            if (_passwordInputField != null) _passwordInputField.text = string.Empty;
        }

        #endregion
    }
}
