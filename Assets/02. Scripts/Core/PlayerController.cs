using UnityEngine;

public class PlayerController : MonoBehaviour
{
    /// <summary>
    /// PlayerObject
    /// 플레이어 캐릭터의 걷기/멈춤 상태 및 속도를 제어합니다.
    /// KeyboardInput 또는 SensorInputProcessor에서 받은 이동 명령을 처리합니다.
    /// </summary>
    
    
    void Start()
    {
        
    }

    public void Move(Vector3 move)
    {
        transform.position += move;
    }
    
    void Update()
    {
        
    }
}
