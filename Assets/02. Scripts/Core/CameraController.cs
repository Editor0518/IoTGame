using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public ArduinoSerialManager serialManager;

    [Header("Settings")]
    public bool useSensor = true;
    
    public enum SensorAxis { X, Y, Z }
    
    [Header("Axis Mapping")]
    [Tooltip("좌우(Yaw) 회전에 사용할 센서 축")]
    public SensorAxis yawAxis = SensorAxis.Z; 
    public bool invertYaw = false; 

    [Tooltip("상하(Pitch) 회전에 사용할 센서 축")]
    public SensorAxis pitchAxis = SensorAxis.X; 
    public bool invertPitch = true; 

    [Header("Angle Limits (각도 제한)")]
    [Tooltip("위로 올려다볼 수 있는 최대 각도 (음수 값, 예: -80)")]
    public float minVerticalAngle = -80f; 
    [Tooltip("아래로 내려다볼 수 있는 최대 각도 (양수 값, 예: 80)")]
    public float maxVerticalAngle = 80f;

    [Header("Sensitivity")]
    public float yawSensitivity = 100.0f;   
    public float pitchSensitivity = 100.0f; 
    
    [Header("Smoothing")]
    public float smoothTime = 0.1f;    

    [Header("Debug")]
    public float currentInputYaw = 0f;   
    public float currentInputPitch = 0f; 
    public float currentPitchAngle = 0f; // 현재 적용된 상하 각도 확인용

    private Camera cam;
    private float yaw = 0f;   
    private float pitch = 0f; 
    private float roll = 0f;  

    private bool isCalibrating = false;
    private float gyroBiasX = 0f;
    private float gyroBiasY = 0f;
    private float gyroBiasZ = 0f;
    
    void Start()
    {
        cam = Camera.main;
        
        if (serialManager == null)
            serialManager = FindFirstObjectByType<ArduinoSerialManager>();

        useSensor = InputManager.currentInputType.Equals(InputManager.InputType.SENSOR);
        
        if (useSensor)
            StartCoroutine(CalibrateSensor(2.0f));
        else
        {
            Vector3 currentEuler = cam.transform.localRotation.eulerAngles;
            yaw = currentEuler.y;
            // 0~360도를 -180~180도로 변환하여 초기값 설정
            pitch = currentEuler.x > 180 ? currentEuler.x - 360 : currentEuler.x;
        }
    }

    IEnumerator CalibrateSensor(float duration)
    {
        isCalibrating = true;
        Debug.Log("센서 보정 중... 평평한 곳에 두세요.");

        float timeout = 3.0f;
        while ((serialManager.GyX == 0 && serialManager.GyY == 0) && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        float elapsed = 0f;
        int count = 0;
        float sumX = 0, sumY = 0, sumZ = 0;

        while (elapsed < duration)
        {
            sumX += serialManager.GyX;
            sumY += serialManager.GyY;
            sumZ += serialManager.GyZ;
            count++;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (count > 0)
        {
            gyroBiasX = sumX / count;
            gyroBiasY = sumY / count;
            gyroBiasZ = sumZ / count;
        }

        isCalibrating = false;
        Debug.Log($"보정 완료! Bias -> X:{gyroBiasX:F2}, Y:{gyroBiasY:F2}, Z:{gyroBiasZ:F2}");
    }

    void Update()
    {
        if (useSensor)
        {
            if (serialManager != null && !isCalibrating) 
                ProcessSensorInput();
        }
        
        ApplyRotation();
    }
    
    float GetSensorValue(SensorAxis axis)
    {
        switch (axis)
        {
            case SensorAxis.X: return serialManager.GyX - gyroBiasX;
            case SensorAxis.Y: return serialManager.GyY - gyroBiasY;
            case SensorAxis.Z: return serialManager.GyZ - gyroBiasZ;
            default: return 0;
        }
    }

    void ProcessSensorInput()
    {
        float rawYaw = GetSensorValue(yawAxis);
        float rawPitch = GetSensorValue(pitchAxis);

        if (Mathf.Abs(rawYaw) < 0.05f) rawYaw = 0;
        if (Mathf.Abs(rawPitch) < 0.05f) rawPitch = 0;

        currentInputYaw = rawYaw * (invertYaw ? -1 : 1) * yawSensitivity;
        currentInputPitch = rawPitch * (invertPitch ? -1 : 1) * pitchSensitivity;

        yaw += currentInputYaw * Time.deltaTime;
        pitch += currentInputPitch * Time.deltaTime;
        
        // ⭐️ [핵심] 상하 각도(Pitch)를 Min/Max 범위 내로 제한 (Clamp)
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        currentPitchAngle = pitch; // 디버그용 표시
    }

    public void Rotate(Vector3 euler)
    {
        if (useSensor) return; 

        float sensitivity = 500f * Time.deltaTime;
        yaw += euler.y * sensitivity;
        pitch += euler.x * sensitivity;

        // 마우스 입력 시에도 각도 제한 적용
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
    }
    
    private void ApplyRotation()
    {
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, roll);
        cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, targetRotation, smoothTime * 10 * Time.deltaTime);
    }
}