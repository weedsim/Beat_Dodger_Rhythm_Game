using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Mirror;

public class LoginUIManager : MonoBehaviour
{
    public static LoginUIManager Instance;

    [Header("UI 패널들")]
    public GameObject loginPanel;
    public GameObject lobbyPanel;
    public GameObject registerPanel;

    [Header("LobbyText")]
    public TextMeshProUGUI[] playerStatusTexts;

    private void Awake()
    {
        Instance = this;
    }

    public void ShowLogin()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        lobbyPanel.SetActive(false);
    }
    // 2. 로그인 화면에서 '회원가입' 버튼 눌렀을 때 회원가입 화면 켜기
    public void ShowRegister()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    // 3. 로그인 성공 시 로비 화면 켜기
    public void ShowLobby()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    // 4. 레디 완료 후 게임 시작
    public void StartGame()
    {
        lobbyPanel.SetActive(false);
        Debug.Log("UI 매니저: 게임 화면으로 전환 완료!");
    }
    public void UpdatePlayerStatus(int playerIndex, bool isReady)
    {
        // 배열 범위를 넘지 않게 안전장치
        if (playerIndex >= 0 && playerIndex < playerStatusTexts.Length)
        {
            if (isReady)
                playerStatusTexts[playerIndex].text = $"Player {playerIndex + 1}: <color=green>Ready!</color>";
            else
                playerStatusTexts[playerIndex].text = $"Player {playerIndex + 1}: <color=red>Not Ready</color>";
        }
    }
    public void OnClickReadyButton()
    {
        // NetworkClient.localPlayer는 미러 서버에 접속된 '나의 캐릭터'를 찾는 마법이옵니다!
        if (NetworkClient.localPlayer != null)
        {
            LobbyPlayer myPlayer = NetworkClient.localPlayer.GetComponent<LobbyPlayer>();
            if (myPlayer != null)
            {
                myPlayer.CmdToggleReady(); // 내 캐릭터에게 레디 신호를 보냅니다요!
            }
        }
        else
        {
            Debug.LogError("주인님! 아직 서버에 접속(Start Host/Client)을 안 하셔서 캐릭터가 없사옵니다!");
        }
    }
}
