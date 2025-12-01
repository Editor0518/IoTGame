using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    ///모든 입력 스크립트를 관리하고, 현재 활성화된 입력 모드(키보드/센서)를 제어합니다.
    /// 싱글톤, 메인

    [System.Serializable]
    public enum InputType
    {
        KEYBOARD, SENSOR
    }
    
    
    public InputType inputType = InputType.KEYBOARD;
    public static InputType currentInputType;
    public static InputManager instance;
    
    public enum InteractState
    {
        NONE, TO_UP, TO_DOWN, TO_LEFT, TO_RIGHT
    }
    
    PlayerController player;
    CameraController cam;
    InteractionHandler handler;

    private void Awake()
    {
        instance = this;
        
        currentInputType = inputType;
    }

    private void Start()
    {
        player = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        // CameraController 컴포넌트는 메인 카메라에 붙어있어야 합니다.
        cam = Camera.main.GetComponent<CameraController>();
        handler = player.transform.GetComponent<InteractionHandler>();

        // CameraController가 카메라에 붙어있지 않고 Player에 붙어있다면 아래와 같이 수정해야 합니다.
        // cam = GameObject.FindWithTag("Player").GetComponentInChildren<CameraController>();
        
        if (cam == null)
        {
            Debug.LogError("CameraController를 찾을 수 없습니다. 메인 카메라에 연결되어 있는지 확인하세요.");
        }
    }

    // rotateMultiply를 제거하거나 1.0f로 설정하여 CameraController의 감도 변수만 사용하도록 단순화
    // private float rotateMultiply = 0.1f; 
    private float moveMultiply = 0.02f;
    public void OnRotateView(Vector3 eulerAngles)
    {
        // KeyboardInput에서 이미 순수한 델타 값을 전달하므로, 여기서는 곱셈을 제거합니다.
        // cam.Rotate(eulerAngles * rotateMultiply);
        cam.Rotate(eulerAngles);
    }

    public void OnMoveCharacter(Vector3 input)
    {
        player.Move(input*moveMultiply);
    }
    
    public void OnInteractTriggered(InteractState state)
    {
        switch (state)
        {
            case InteractState.TO_UP:
               // handler.ActionHit();
                break;
            case InteractState.TO_DOWN:
                //handler.ActionHit();
                break;
            case InteractState.TO_LEFT:
               // handler.ActionOpenDoor();
                break;
            case InteractState.TO_RIGHT:
               // handler.ActionOpenDoor();
                break;
        }
        
        
    }
    
}