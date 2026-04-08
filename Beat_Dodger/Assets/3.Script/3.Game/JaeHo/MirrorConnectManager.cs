using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using TMPro;

public class MirrorConnectManager : MonoBehaviour
{
    public TMP_InputField ipInputField;

    public void OnClickConnectServerButton()
    {
        // 1. 입력창에 적힌 IP 주소 가져오기
        string ipAddress = ipInputField.text;

        // 만약 아무것도 안 적었으면 기본값인 내 컴퓨터(localhost)로 설정!
        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "localhost";
        }

        NetworkManager.singleton.networkAddress = ipAddress;

        Debug.Log($"[{ipAddress}] 서버로 접속 시도 중...");
        NetworkManager.singleton.StartClient();
    }
}
