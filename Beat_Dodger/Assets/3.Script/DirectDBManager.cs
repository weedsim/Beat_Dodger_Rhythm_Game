using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using MySql.Data.MySqlClient;
using Mirror;

public class DirectDBManager : MonoBehaviour
{
    private string connectionString = "Server=localhost;Database=MyGameDB;Uid=root;Pwd=1234;Port=3307;";

    public TMP_InputField idInputField;
    public TMP_InputField pwInputField;

    // 1. 회원가입 기능
    public void OnClickRegisterButton()
    {
        string inputId = idInputField.text;
        string inputPw = pwInputField.text;
        RegisterUserAsync(inputId, inputPw);
    }

    private async void RegisterUserAsync(string id, string pw)
    {
        Debug.Log("DB connecting.....");
        await Task.Run(() =>
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
                        if (result > 0)
                        {
                            Debug.Log($"[성공] {id}님 회원가입 완료.");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[에러 발생] DB 연결 실패: " + ex.Message);
                }
            }
        });
    }

    // 2. 로그인 & 미러 서버 접속 기능
    public async void OnClickLoginButton()
    {
        string inputId = idInputField.text;
        string inputPw = pwInputField.text;

        Debug.Log("DB에서 유저 정보 찾는 중...");

        // 1. DB에서 비밀번호 맞는지 백그라운드에서 확인
        bool isLoginSuccess = await Task.Run(() => CheckLoginDB(inputId, inputPw));

        // 2. 로그인 성공 시, 유니티 메인 스레드에서 미러 서버로 접속!
        if (isLoginSuccess)
        {
            Debug.Log($"[로그인 성공]  {inputId}님! 게임 서버로 이동합니다!");

            // 미러 매니저에게 목적지를 알려주고 클라이언트 실행!
            NetworkManager.singleton.networkAddress = "localhost";
            NetworkManager.singleton.StartHost();
        }
        else
        {
            Debug.LogWarning("[로그인 실패]  아이디 또는 비밀번호가 틀렸습니다.");
        }
    }

    // 실제 DB와 통신하여 비밀번호를 검증하는 로직
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
                            if (dbPassword == pw) return true; // 비번 일치!
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[에러] DB 연결 실패: " + ex.Message);
            }
        }
        return false; // 뭔가 틀렸거나 에러남
    }
}