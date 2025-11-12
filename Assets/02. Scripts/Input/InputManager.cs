using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    ///모든 입력 스크립트를 관리하고, 현재 활성화된 입력 모드(키보드/센서)를 제어합니다.
    /// 싱글톤, 메인

    enum InputType
    {
        KEYBOARD, SENSOR
    }
    
    
    InputType inputType = InputType.KEYBOARD;
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
        
    }

    private void Start()
    {
        player = GameObject.FindWithTag("Player").GetComponent<PlayerController>();
        cam = Camera.main.transform.GetComponent<CameraController>();
        handler = player.transform.GetComponent<InteractionHandler>();
    }

    private float rotateMultiply = 0.1f;
    private float moveMultiply = 0.02f;
    public void OnRotateView(Vector3 eulerAngles)
    {
        cam.Rotate(eulerAngles*rotateMultiply);
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
                handler.ActionHit();
                break;
            case InteractState.TO_DOWN:
                handler.ActionHit();
                break;
            case InteractState.TO_LEFT:
                handler.ActionOpenDoor();
                break;
            case InteractState.TO_RIGHT:
                handler.ActionOpenDoor();
                break;
        }
        
        
    }
    
}
