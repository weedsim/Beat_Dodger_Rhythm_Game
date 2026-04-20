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
        ShowLogin();
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
        Debug.Log("ShowLobby 호출됨! " + System.Environment.StackTrace);
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
        // localPlayer 대신 씬에서 직접 내 LobbyPlayer 찾기
        LobbyPlayer myPlayer = null;

        foreach (var p in FindObjectsOfType<LobbyPlayer>())
        {
            if (p.isLocalPlayer)
            {
                myPlayer = p;
                break;
            }
        }

        if (myPlayer != null)
        {
            myPlayer.CmdToggleReady();
        }
        else
        {
            Debug.LogError("LobbyPlayer를 찾을 수 없어요! 서버에 연결됐는지 확인하세요.");
        }
    }
}
