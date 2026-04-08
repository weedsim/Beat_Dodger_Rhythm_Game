using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using MySql.Data.MySqlClient;
using Mirror;

public class DirectDBManager : MonoBehaviour
{
    private string connectionString = "Server=localhost;Database=MyGameDB;Uid=root;Pwd=1234;Port=3307;";

    [Header("LoginUI Settings")]
    public TMP_InputField loginIdInput;
    public TMP_InputField loginPwInput;

    [Header("RegisterUI Settings")]
    public TMP_InputField registerIdInput;
    public TMP_InputField registerPwInput;

    public async void OnClickSubmitRegister()
    {
        string inputId = registerIdInput.text;
        string inputPw = registerPwInput.text;

        // DB 작업이 끝날 때까지 대기
        bool isSuccess = await Task.Run(() => DoRegisterDB(inputId, inputPw));

        if (isSuccess)
        {
            Debug.Log($"[회원가입 성공] 환영합니다! 로그인을 진행해주세요.");

            // 1. 로그인 창으로 돌아가기
            LoginUIManager.Instance.ShowLogin();

            // 2. 센스있게 방금 가입한 아이디를 로그인 창에 미리 적어주기!
            loginIdInput.text = inputId;
            loginPwInput.text = ""; // 비밀번호는 초기화

            // 3. 회원가입 창에 적었던 글씨들은 싹 지워주기
            registerIdInput.text = "";
            registerPwInput.text = "";
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
                    return result > 0; // 성공하면 true 반환
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[에러 발생] 회원가입 실패: " + ex.Message);
                return false;
            }
        }
    }

    // ==========================================
    // 2. 로그인 기능 (Login)
    // ==========================================
    public async void OnClickLoginButton()
    {
        string inputId = loginIdInput.text;
        string inputPw = loginPwInput.text;

        bool isLoginSuccess = await Task.Run(() => CheckLoginDB(inputId, inputPw));

        if (isLoginSuccess)
        {
            Debug.Log($"[로그인 성공] {inputId}님! 접속합니다!");
            LoginUIManager.Instance.ShowLobby();

            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartClient();
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
                Debug.LogError("[에러] DB 연결 실패: " + ex.Message);
            }
        }
        return false;
    }
}