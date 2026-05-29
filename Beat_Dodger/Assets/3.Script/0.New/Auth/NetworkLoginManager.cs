using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using MySql.Data.MySqlClient;
using Mirror;

// NetworkBehaviour를 상속받아야 Cmd와 TargetRpc를 사용할 수 있습니다.
public class NetworkLoginManager : NetworkBehaviour
{
    // DB 접속 정보는 오직 서버에서만 사용됩니다. (private으로 설정)
    private string connectionString = "Server=localhost;Database=MyGameDB;Uid=root;Pwd=1234;Port=3307;";

    [Header("LoginUI Settings")]
    public TMP_InputField loginIdInput;
    public TMP_InputField loginPwInput;

    [Header("RegisterUI Settings")]
    public TMP_InputField registerIdInput;
    public TMP_InputField registerPwInput;

    // ==========================================
    // 1. 회원가입 흐름
    // ==========================================

    // 클라이언트 UI 버튼 이벤트
    public void OnClickSubmitRegister()
    {
        string inputId = registerIdInput.text;
        string inputPw = registerPwInput.text;

        // 서버에게 회원가입을 요청합니다.
        CmdRequestRegister(inputId, inputPw);
    }

    // [Command] 클라이언트가 호출하지만, 실제 실행은 서버에서 됩니다.
    [Command]
    private void CmdRequestRegister(string id, string pw)
    {
        // 서버에서 DB 작업을 수행 (비동기로 처리하여 서버 멈춤 방지)
        Task.Run(() =>
        {
            bool isSuccess = DoRegisterDB(id, pw);

            // 결과를 요청한 특정 클라이언트에게만 전송합니다.
            TargetNotifyRegisterResult(connectionToClient, isSuccess, id);
        });
    }

    // [TargetRpc] 서버가 특정 클라이언트에게만 메시지를 보냅니다.
    [TargetRpc]
    private void TargetNotifyRegisterResult(NetworkConnection target, bool success, string id)
    {
        if (success)
        {
            Debug.Log($"[회원가입 성공] 환영합니다!");

            // UI 처리 (LoginUIManager가 싱글톤이라고 가정)
            // LoginUIManager.Instance.ShowLogin(); 

            loginIdInput.text = id;
            loginPwInput.text = "";
            registerIdInput.text = "";
            registerPwInput.text = "";
        }
        else
        {
            Debug.LogError("[회원가입 실패] 이미 존재하는 아이디거나 DB 오류가 발생했습니다.");
        }
    }

    private bool DoRegisterDB(string id, string pw)
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            try
            {
                conn.Open();
                string query = "INSERT INTO Users (Username, PasswordHash) VALUES(@user, @pwd)";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@user", id);
                    cmd.Parameters.AddWithValue("@pwd", pw);
                    int result = cmd.ExecuteNonQuery();
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[서버 DB 에러] 회원가입 중 오류: " + ex.Message);
                return false;
            }
        }
    }

    // ==========================================
    // 2. 로그인 흐름
    // ==========================================

    // 클라이언트 UI 버튼 이벤트
    public void OnClickLoginButton()
    {
        string inputId = loginIdInput.text;
        string inputPw = loginPwInput.text;

        // 서버에게 로그인을 요청합니다.
        CmdRequestLogin(inputId, inputPw);
    }

    // [Command] 서버에서 실행
    [Command]
    private void CmdRequestLogin(string id, string pw)
    {
        Task.Run(() =>
        {
            bool isLoginSuccess = CheckLoginDB(id, pw);

            // 결과를 요청한 클라이언트에게 전송
            TargetNotifyLoginResult(connectionToClient, isLoginSuccess, id);
        });
    }

    // [TargetRpc] 클라이언트에서 실행
    [TargetRpc]
    private void TargetNotifyLoginResult(NetworkConnection target, bool success, string id)
    {
        if (success)
        {
            Debug.Log($"[로그인 성공] {id}님! 환영합니다.");
            // 여기서부터 실제 게임 진입 로직을 작성하세요.
            // 예: RhythmNetworkManager.Instance.isLoggedIn = true;
            // SceneManager.LoadScene("GameScene");
        }
        else
        {
            Debug.LogWarning("[로그인 실패] 아이디 또는 비밀번호가 틀렸습니다.");
        }
    }

    private bool CheckLoginDB(string id, string pw)
    {
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            try
            {
                conn.Open();
                string query = "SELECT PasswordHash FROM Users WHERE Username = @user";
                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@user", id);
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string dbPassword = reader.GetString(0);
                            if (dbPassword == pw) return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[서버 DB 에러] 로그인 중 오류: " + ex.Message);
            }
        }
        return false;
    }
}