using UnityEngine;

public class KeyboardInput : MonoBehaviour
{
    ///키보드 입력을 감지하여 PlayerController에 명령을 전달합니다.
    /// 임시 테스트 목적으로, 나중에 SensorInput으로 대체합니다.
    
    // 이 변수는 더 이상 CameraController 내부에서 처리되므로 사용하지 않습니다. (주석 처리 또는 제거 가능)
    // [SerializeField] private float mouseSensitivity = 20f;
    
    // 카메라 회전을 잠글 상태 변수 (이 로직은 센서와 함께 쓰일 때만 유효)
    private bool isRotationLocked = false;
    // 카메라 회전을 잠그는 시간 (초)
    [SerializeField] private float lockoutDuration = 0.5f;
    // 잠금 해제 시간이 저장되는 변수
    private float rotationLockoutEndTime = 0f;

    private InteractionHandler interactionHandler;
    
    void Start()
    {
        // InteractionHandler 참조
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            interactionHandler = player.GetComponent<InteractionHandler>();
        }
    }
    
    void Update()
    {
        // InputManager의 현재 입력 타입이 KEYBOARD인지 확인 (선택 사항)
        if (InputManager.currentInputType != InputManager.InputType.KEYBOARD)
        {
            // 센서 입력 모드일 경우 키보드 움직임만 처리
            ProcessMovement();
            // 마우스 회전은 처리하지 않음
            return; 
        }

        // 1. 캐릭터 이동 (W/A/S/D 키) - 기존 로직 유지
        ProcessMovement();

        // 2. 마우스 입력 처리 (시점 회전)
        ProcessMouseRotation();
        
        // 3. 마우스 왼쪽 클릭 (상호작용 - 잡기/놓기)
        if (Input.GetMouseButtonDown(0) && interactionHandler != null)
        {
            // 마우스 클릭 시 InteractionHandler에 상호작용 시도 요청
            interactionHandler.TryInteract();
        }

        // **이전의 '휙 움직여서 상호작용 감지' 로직은 Cooking Simulator 스타일에 맞게 제거되었습니다.**
    }

    private void ProcessMovement()
    {
        Vector3 moveDirection = Vector3.zero;

        // Note: W/A/S/D 이동은 카메라의 Forward 방향을 기준으로 처리하는 것이 일반적이지만,
        // 현재 코드는 Vector3.forward/back/left/right를 사용하고 있어 그대로 유지합니다.
        // 만약 카메라 시점 기준으로 움직이고 싶다면, InputManager에서 회전 정보를 적용해야 합니다.
        if (Input.GetKey(KeyCode.W))
        {
            moveDirection += Vector3.forward;
        }
        if (Input.GetKey(KeyCode.S))
        {
            moveDirection += Vector3.back;
        }
        if (Input.GetKey(KeyCode.A))
        {
            moveDirection += Vector3.left;
        }
        if (Input.GetKey(KeyCode.D))
        {
            moveDirection += Vector3.right;
        }

        if (moveDirection != Vector3.zero)
        {
            InputManager.instance.OnMoveCharacter(moveDirection.normalized);
        }
        else
        {
            InputManager.instance.OnMoveCharacter(Vector3.zero);
        }
    }

    private void ProcessMouseRotation()
    {
        // 1. 회전 잠금 해제 타이머 확인 (센서 입력에서 유용)
        if (isRotationLocked && Time.time >= rotationLockoutEndTime)
        {
            isRotationLocked = false;
        }

        // 2. 시점 회전 (잠금 상태가 아닐 때만 실행)
        if (!isRotationLocked) 
        {
            // GetAxis는 이미 프레임 간의 델타 값을 반환합니다.
            // mouseSensitivity를 제거하고 순수한 델타 값만 사용합니다.
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // X축(Pitch)은 마우스 Y축 이동, Y축(Yaw)은 마우스 X축 이동
            // Input.GetAxis("Mouse Y")는 위로 움직일 때 양수, 아래로 움직일 때 음수입니다. 
            // Pitch는 위로 볼 때 각도가 감소해야 하므로 부호를 반전합니다. (-mouseY)
            Vector3 rotationDelta = new Vector3(-mouseY, mouseX, 0f); 

            InputManager.instance.OnRotateView(rotationDelta);
        }
    }
}