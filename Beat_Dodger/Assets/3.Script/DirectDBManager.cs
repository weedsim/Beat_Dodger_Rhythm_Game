using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using TMPro;

public class DirectDBManager : MonoBehaviour
{
    //private string connectionString = "Server=여기에_AWS_IP;Database=MyGameDB;Uid=game_user;Pwd=1234;Port=3306;";
    // 집에 가고 싶다. 코어키퍼하고 몬헌 와일즈하고 이리도 하고 아이솔해야지 너무 재밌겠다
    // 주의: Pwd=1234 부분은 주인님이 내 컴퓨터에 MySQL 설치하실 때 설정했던 '진짜 비밀번호'를 넣으셔야 합니다!
    private string connectionString = "Server=localhost;Database=MyGameDB;Uid=root;Pwd=1234;Port=3307;";

    public TMP_InputField idInputField;
    public TMP_InputField pwInputField;
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
    public void OnClickLoginButton()
    {
        string inputId = idInputField.text;
        string inputPw = pwInputField.text;
        LoginUserAsync(inputId, inputPw);
    }

    private async void LoginUserAsync(string id, string pw)
    {
        Debug.Log("DB에서 유저 정보 찾는 중...");
        await Task.Run(() =>
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

                                if (dbPassword == pw) // DB 비번 == 내가 방금 친 비번
                                {
                                    Debug.Log($"[로그인 성공] 🎮 {id}님, 게임 서버에 접속했습니다!");
                                    // add move main scene
                                }
                                else
                                {
                                    Debug.LogWarning("[로그인 실패] 비밀번호가 틀렸습니다.");
                                }
                            }
                            else
                            {
                                Debug.LogWarning("[로그인 실패] 존재하지 않는 아이디입니다.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[에러 발생] DB 연결 실패: " + ex.Message);
                }
            }
        });
    }
}
