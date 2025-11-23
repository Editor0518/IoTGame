using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    [Header("References")]
    public ArduinoSerialManager serialManager;

    [Header("Settings")]
    public bool useSensor = true;
    public float rotationSpeed = 1.0f; // 자이로 회전 속도 보정값
    
    [Header("Smoothing")]
    public float smoothTime = 0.1f; // 움직임 부드럽게 (0.1 ~ 0.2 추천)

    [Header("Debug")]
    public bool isCalibrating = false;
    public bool showDebugLog = false;

    private Camera cam;
    
    // 회전값 누적 변수
    private float yaw = 0f;   // 좌우 (Y축, 자이로 사용)
    private float pitch = 0f; // 상하 (X축, 가속도+자이로 융합 or 가속도 단독)
    private float roll = 0f;  // 기울기 (Z축) - 0으로 고정됨

    // 캘리브레이션용 오차값 변수
    private float gyroBiasX = 0f;
    private float gyroBiasY = 0f;
    private float gyroBiasZ = 0f;

    // 부드러운 회전을 위한 속도 변수 (SmoothDamp용)
    private Vector3 currentVelocity;

    void Start()
    {
        cam = Camera.main;
        
        if (serialManager == null)
            serialManager = FindFirstObjectByType<ArduinoSerialManager>();

        // 시작하자마자 센서 0점 조절 시작
        if (useSensor)
            StartCoroutine(CalibrateSensor(2.0f)); // 2초 동안 보정
    }

    // 1. 센서 보정 코루틴 (시작 시 2초간 움직이지 마세요!)
    IEnumerator CalibrateSensor(float duration)
    {
        isCalibrating = true;
        Debug.Log("센서 보정 중... 센서를 평평한 곳에 가만히 두세요.");

        float elapsed = 0f;
        int count = 0;
        float sumX = 0, sumY = 0, sumZ = 0;

        // 데이터가 들어올 때까지 잠시 대기
        while (serialManager.GyX == 0 && serialManager.GyY == 0) 
            yield return null;

        while (elapsed < duration)
        {
            sumX += serialManager.GyX;
            sumY += serialManager.GyY;
            sumZ += serialManager.GyZ;
            count++;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 평균 오차값 저장
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
        if (!useSensor || serialManager == null || isCalibrating) return;

        ProcessSensorInput();
    }

    void ProcessSensorInput()
    {
        // --- 1. 자이로 데이터 읽기 및 오차 보정 ---
        // 아두이노에서 /16384.0 처리가 되어 있다고 가정
        float rawGyX = serialManager.GyX - gyroBiasX;
        float rawGyY = serialManager.GyY - gyroBiasY;
        float rawGyZ = serialManager.GyZ - gyroBiasZ;

        // Deadzone (너무 작은 떨림 무시)
        if (Mathf.Abs(rawGyX) < 0.05f) rawGyX = 0;
        if (Mathf.Abs(rawGyY) < 0.05f) rawGyY = 0;
        if (Mathf.Abs(rawGyZ) < 0.05f) rawGyZ = 0;

        // --- 2. 가속도 데이터 읽기 (절대 각도 계산용) ---
        float accX = serialManager.AcX;
        float accY = serialManager.AcY;
        float accZ = serialManager.AcZ;

        // --- 3. 회전 계산 ---

        // [Yaw - 좌우] 자이로 적분 (가속도로는 좌우 방위를 알 수 없음)
        // GyZ를 사용할지 GyY를 사용할지는 센서 부착 방향에 따라 다름 (현재 Z축 기준)
        yaw += -rawGyZ * rotationSpeed * Time.deltaTime; 

        // [Pitch - 상하] 가속도 센서 이용 (절대 각도)
        // atan2를 이용해 중력 방향에 대한 각도를 구함
        // 센서 방향에 따라 accY, accZ 순서나 부호가 바뀔 수 있음
        float targetPitch = Mathf.Atan2(accY, accZ) * Mathf.Rad2Deg;
        
        // [Roll - 기울기] - 기능 비활성화 (0으로 고정)
        // 화면이 기울어지는 것을 막기 위해 계산 로직을 주석 처리하고 0으로 고정합니다.
        /* float targetRoll = Mathf.Atan2(-accX, accZ) * Mathf.Rad2Deg;
        roll = Mathf.LerpAngle(roll, targetRoll, 0.1f);
        */
        roll = 0f; // 강제로 0도 고정

        // 자이로와 가속도 섞기 (Pitch만 적용)
        pitch = Mathf.LerpAngle(pitch, targetPitch, 0.1f); // 0.1f는 반응속도

        // 각도 제한 (목 꺾임 방지)
        // pitch = Mathf.Clamp(pitch, -80f, 80f); 

        // --- 4. 최종 회전 적용 ---
        // Roll(Z축)은 항상 0으로 입력됨
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, roll);
        
        // 부드럽게 회전 (SmoothDamp)
        cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, targetRotation, smoothTime * 10 * Time.deltaTime);

        if (showDebugLog)
        {
            Debug.Log($"Pitch: {pitch:F1}, Yaw: {yaw:F1}, Roll: {roll:F1}");
        }
    }
    
    // 외부 강제 회전 (필요 시)
    public void Rotate(Vector3 euler)
    {
        yaw += euler.y;
        pitch += euler.x;
    }
}