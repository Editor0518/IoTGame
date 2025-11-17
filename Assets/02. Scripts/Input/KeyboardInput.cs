using UnityEngine;

public class KeyboardInput : MonoBehaviour
{
    ///키보드 입력을 감지하여 PlayerController에 명령을 전달합니다.
    /// 임시 테스트 목적으로, 나중에 SensorInput으로 대체합니다.
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    
    // 마우스 회전 속도를 조절하기 위한 변수
    [SerializeField] private float mouseSensitivity = 20f;
    
    // 카메라 회전을 잠글 상태 변수
    private bool isRotationLocked = false;
    // 카메라 회전을 잠그는 시간 (초)
    [SerializeField] private float lockoutDuration = 0.5f;
    // 잠금 해제 시간이 저장되는 변수
    private float rotationLockoutEndTime = 0f;

    void Update()
    {
        // 1. 캐릭터 이동 (W/A/S/D 키) - 기존 로직 유지
        Vector3 moveDirection = Vector3.zero;

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

        // ----------------------------------------------------

        // **2-1. 회전 잠금 해제 타이머 확인**
        if (isRotationLocked && Time.time >= rotationLockoutEndTime)
        {
            isRotationLocked = false;
            // Debug.Log("카메라 회전 잠금 해제.");
        }

        // **2-2. 마우스 입력 처리 (시점 회전 및 상호작용)**
        Cursor.lockState = CursorLockMode.Locked;
        
       // Cursor.visible = false;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        Vector3 rotationDelta = new Vector3(-mouseY, mouseX, 0f);

        // 상호작용 임계값 (테스트를 위해 임시로 설정)
        const float INTERACT_THRESHOLD = 40f;

        // 3. 상호작용 감지 및 카메라 잠금 실행
        bool interactionDetected = false;
        InputManager.InteractState interactState = InputManager.InteractState.NONE;

        if (rotationDelta.x > INTERACT_THRESHOLD)
        {
            interactState = InputManager.InteractState.TO_DOWN; // 위에서 아래로 휙 내렸음
            interactionDetected = true;
        }
        else if (rotationDelta.x < -INTERACT_THRESHOLD)
        {
            interactState = InputManager.InteractState.TO_UP; // 아래에서 위로 휙 올렸음
            interactionDetected = true;
        }
        else if (rotationDelta.y > INTERACT_THRESHOLD)
        {
            interactState = InputManager.InteractState.TO_RIGHT; // 왼쪽에서 오른쪽으로 휙 움직였음
            interactionDetected = true;
        }
        else if (rotationDelta.y < -INTERACT_THRESHOLD)
        {
            interactState = InputManager.InteractState.TO_LEFT; // 오른쪽에서 왼쪽으로 휙 돌렸음
            interactionDetected = true;
        }
        
        if (interactionDetected&&!isRotationLocked)
        {
            // 상호작용 이벤트 발생
            InputManager.instance.OnInteractTriggered(interactState);
            
            // 카메라 회전 잠금 설정
            isRotationLocked = true;
            rotationLockoutEndTime = Time.time + lockoutDuration;
//            Debug.Log($"상호작용 발생. 회전 잠금: {lockoutDuration}초");
        }
        // ----------------------------------------------------
        
        // 4. 시점 회전 (잠금 상태가 아닐 때만 실행)
        // 상호작용이 감지되지 않았고, 현재 잠금 상태가 아닐 때만 회전 이벤트를 전달
        else if (!isRotationLocked) 
        {
            InputManager.instance.OnRotateView(rotationDelta);
        }
        
        // 잠금 상태일 때는 아무 일도 하지 않으므로, 카메라 시점 회전이 멈춤
    }
}
