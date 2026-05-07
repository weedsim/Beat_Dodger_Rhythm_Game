using UnityEngine;
using Mirror;

public class DeadReckoningTest : NetworkBehaviour
{
    [Header("R&D 설정")]
    [Tooltip("일부러 렉을 유발하는 시간 (0.2초마다 서버로 전송)")]
    public float customSyncInterval = 0.2f;
    private float _syncTimer;

    // 서버가 기억하고, 남들에게 뿌려주는 '마지막 정보'
    [SyncVar] private Vector3 _lastKnownPosition;
    [SyncVar] private Vector3 _lastKnownVelocity;

    private Vector3 _currentVelocity;

    void Update()
    {
        // 1. 내가 조종하는 캐릭터일 때 (진짜 움직임)
        if (isLocalPlayer)
        {
            // 방향키 입력 받아서 직접 이동
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _currentVelocity = new Vector3(h, 0, v).normalized * 5f; // 속도 5

            transform.position += _currentVelocity * Time.deltaTime;

            // 일부러 데이터를 띄엄띄엄(0.2초마다) 보냅니다!
            _syncTimer += Time.deltaTime;
            if (_syncTimer >= customSyncInterval)
            {
                CmdSyncData(transform.position, _currentVelocity);
                _syncTimer = 0f;
            }
        }
        // 2. 남의 캐릭터를 볼 때 (✨ 여기가 데드 레커닝 핵심!)
        else
        {
            // 통신이 안 올 때 멈춰있는 게 아니라, "아까 그 속도대로 가고 있겠지!" 하고 내 맘대로 밀어버립니다!
            transform.position += _lastKnownVelocity * Time.deltaTime;

            // R&D 포인트: 
            // 나중에 진짜 위치(_lastKnownPosition)와 지금 내 맘대로 예측한 위치가 너무 차이 나면
            // Vector3.Lerp로 부드럽게 끌어당겨서 보정하는 로직을 여기에 추가하시면 완벽하옵니다!
        }
    }

    // 클라이언트 -> 서버로 내 위치와 속도를 보고하는 명령
    [Command]
    void CmdSyncData(Vector3 pos, Vector3 vel)
    {
        _lastKnownPosition = pos;
        _lastKnownVelocity = vel;
    }
}