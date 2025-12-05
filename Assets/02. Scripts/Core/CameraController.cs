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

    
    [Header("Angle Calculation")]
    [Tooltip("가속도 센서 기반 회전 사용 여부 (MPU6500)")]
    public bool useAccelerometerForPitchRoll = true;
    
    
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
    
    // GetSensorValue 함수는 Gyroscope 값만 반환하도록 재정의하거나, 사용하지 않습니다.
    // 기존 GetSensorValue 함수는 Gyroscope 값에서 Bias를 뺀 값을 반환하므로, Yaw 계산에만 사용합니다.
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
        // 1. 자이로스코프를 사용하여 Yaw(수평 회전) 업데이트 (누적 방식 유지)
        float rawYaw = GetSensorValue(yawAxis);
        if (Mathf.Abs(rawYaw) < 0.05f) rawYaw = 0;

        currentInputYaw = rawYaw * (invertYaw ? -1 : 1) * yawSensitivity;
        yaw += currentInputYaw * Time.deltaTime;


        // 2. 가속도 센서를 사용하여 Pitch(상하) 및 Roll(좌우 기울기) 절대 각도 계산
        if (useAccelerometerForPitchRoll)
        {
            // MPU6500의 가속도 값 가져오기
            // AcX, AcY, AcZ는 ±32768 범위의 정수 값입니다.
            // 여기서는 그대로 사용하거나, 중력가속도(g)로 정규화하여 사용합니다. (g = 16384.0 또는 32768.0으로 나눔)
            // Unity에서 이미 float으로 파싱하므로 그대로 사용
            float accX = serialManager.AcX;
            float accY = serialManager.AcY;
            float accZ = serialManager.AcZ;
            
            // Pitch (Y축 회전, 상하 각도) 계산
            // X축 가속도와 (Y-Z평면)의 합을 이용한 atan2 공식
            // Math.Atan2(accX, Math.Sqrt(accY * accY + accZ * accZ))
            // Unity의 MathF.Atan2와 MathF.Sqrt를 사용합니다.
            // 라디안을 Degree로 변환 (* Mathf.Rad2Deg)
            float calculatedPitch = Mathf.Atan2(accX, Mathf.Sqrt(accY * accY + accZ * accZ)) * Mathf.Rad2Deg;
            
            // Roll (Z축 회전, 좌우 기울기) 계산
            // Y축 가속도와 Z축 가속도를 이용한 atan2 공식
            // Math.Atan2(accY, accZ)
            float calculatedRoll = Mathf.Atan2(accY, accZ) * Mathf.Rad2Deg;

            // 3. 축 매핑 및 반전 적용 (사용자의 설정에 맞게)
            
            // Pitch 적용 (min/max 제한)
            // MPU의 Pitch 계산 방향과 Unity의 카메라 축 방향을 맞추기 위해 값을 조정합니다.
            // calculatedPitch는 -90~90 범위이므로, 여기에 invertPitch를 적용합니다.
            // ⭐️ 주의: 센서의 방향과 축 매핑에 따라 calculatedPitch의 부호가 달라질 수 있습니다.
            float mappedPitch = calculatedPitch;
            if(invertPitch) mappedPitch *= -1; // Inspector 설정 적용

            // ⭐️ Pitch는 절대 각도로 덮어씁니다.
            pitch = mappedPitch; 
            
            // Roll은 일단 0으로 설정하거나, 필요한 경우 계산된 Roll을 반영합니다.
            // Roll이 카메라 회전에 영향을 주도록 하려면, 다음 줄을 활성화하세요.
            // roll = calculatedRoll * (invertYaw ? -1 : 1); 
            // ⭐️ 현재 1인칭 카메라에서는 Roll을 0으로 유지하는 것이 일반적이므로 일단 0으로 유지합니다.
            roll = 0f; 
        }

        // ⭐️ 상하 각도(Pitch)를 Min/Max 범위 내로 제한 (Clamp)
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        currentPitchAngle = pitch; // 디버그용 표시
    }
    
    // ... (Rotate, ApplyRotation, OnDestroy 함수는 유지)

    /*
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
*/
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