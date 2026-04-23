using UnityEngine;
using UnityEngine.UI;
using BeatDodger.Network;

namespace BeatDodger.UI
{
    /// <summary>
    /// 로그인 화면의 UI 요소들을 관리하고 사용자 입력을 처리하는 컨트롤러 (Legacy UI 버전)
    /// </summary>
    public class LoginUIController : MonoBehaviour
    {
        #region Variables

        [Header("UI References")]
        [SerializeField] private InputField _idInputField;
        [SerializeField] private InputField _passwordInputField;
        [SerializeField] private Button _loginButton;

        [Header("Dependencies")]
        [SerializeField] private CustomNetworkManager _networkManager;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
        }

        #endregion

#region Public Methods (Client Side)

#if !UNITY_SERVER
        /// <summary>
        /// 로그인 버튼 클릭 시 호출되는 메서드
        /// </summary>
        public void OnLoginButtonClicked()
        {
            string userId = _idInputField != null ? _idInputField.text : string.Empty;
            string password = _passwordInputField != null ? _passwordInputField.text : string.Empty;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[LoginUI] ID 또는 비밀번호가 입력되지 않았습니다.");
                // TODO: 사용자에게 알림을 주는 Legacy UI 팝업 호출 필요
                return;
            }

            if (_networkManager != null)
            {
                Debug.Log($"[LoginUI] Requesting login for: {userId}");
                _networkManager.AttemptLogin(userId, password);
            }
            else
            {
                Debug.LogError("[LoginUI] CustomNetworkManager reference is missing!");
            }
        }
#endif

#endregion
    }
}
