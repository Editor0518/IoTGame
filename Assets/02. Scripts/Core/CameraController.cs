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
    [Tooltip("마우스/키보드 입력의 회전 가속도. 센서 사용 시 무시됨.")]
    public float mouseRotationSpeed = 500f; // 마우스 입력에 사용할 회전 속도 (Degree/s)

    [Header("Debug")]
    public bool isCalibrating = false;
    public bool showDebugLog = true;

    private Camera cam;
    
    // 회전값 누적 변수: 마우스/키보드 입력과 센서 입력 모두 이 변수를 통해 회전됩니다.
    private float yaw = 0f;   // 좌우 (Y축)
    private float pitch = 0f; // 상하 (X축)
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

        // useSensor 변수는 InputManager의 현재 입력 타입에 따라 설정됩니다.
        useSensor = InputManager.currentInputType.Equals(InputManager.InputType.SENSOR);
        
        // 시작하자마자 센서 0점 조절 시작
        if (useSensor)
            StartCoroutine(CalibrateSensor(2.0f)); // 2초 동안 보정
        else
        {
            // 센서를 사용하지 않을 경우, 현재 카메라 회전값을 yaw/pitch에 초기값으로 설정합니다.
            // 이렇게 해야 마우스 회전 시작 시 튀지 않습니다.
            Vector3 currentEuler = cam.transform.localRotation.eulerAngles;
            yaw = currentEuler.y;
            // -180~180 범위로 변환하여 자연스러운 pitch 연산이 되도록 합니다.
            pitch = currentEuler.x > 180 ? currentEuler.x - 360 : currentEuler.x;
        }
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
        
        // 센서 보정 완료 후 초기 카메라 회전값을 yaw/pitch에 설정
        Vector3 currentEuler = cam.transform.localRotation.eulerAngles;
        yaw = currentEuler.y;
        pitch = currentEuler.x > 180 ? currentEuler.x - 360 : currentEuler.x;
    }

    void Update()
    {
        if (useSensor)
        {
            // 센서 입력 모드일 경우 센서 데이터만 처리
            if (serialManager == null || isCalibrating) return;
            ProcessSensorInput();
        }
        // 마우스 입력 모드일 경우, KeyboardInput에서 Rotate 함수를 통해 이미 처리됩니다.

        // 마우스/센서 입력에 관계없이 최종 회전은 Update 끝에서 한 번만 적용합니다.
        ApplyRotation();
    }
/*
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
        roll = 0f; // 강제로 0도 고정

        // 자이로와 가속도 섞기 (Pitch만 적용)
        pitch = Mathf.LerpAngle(pitch, targetPitch, 0.1f); // 0.1f는 반응속도

        // 각도 제한 (목 꺾임 방지)
        // pitch = Mathf.Clamp(pitch, -80f, 80f); // 필요 시 주석 해제

        if (showDebugLog)
        {
            Debug.Log($"[Sensor] Pitch: {pitch:F1}, Yaw: {yaw:F1}, Roll: {roll:F1}");
        }
    }
    */

// ... (기존 Start, CalibrateSensor, Update 등 유지)
    
    void ProcessSensorInput()
    {
        // --- 1. 자이로 데이터 읽기 및 오차 보정 ---
        // 아두이노에서 /16384.0 처리가 되어 있다고 가정 (회전 속도 deg/s)
        float rawGyX = serialManager.GyX - gyroBiasX;
        float rawGyY = serialManager.GyY - gyroBiasY;
        float rawGyZ = serialManager.GyZ - gyroBiasZ;

        // Deadzone (너무 작은 떨림 무시)
        if (Mathf.Abs(rawGyX) < 0.05f) rawGyX = 0;
        if (Mathf.Abs(rawGyY) < 0.05f) rawGyY = 0;
        if (Mathf.Abs(rawGyZ) < 0.05f) rawGyZ = 0;

        // --- 2. 회전 계산 (자이로 적분) ---

        // [Yaw - 좌우]: Y축 회전. GyZ 또는 GyY를 사용 (아두이노 코드 [cite: 30] 기준으로 Z축을 Yaw로 가정하고 진행)
        // 회전 속도 * Time.deltaTime = 회전 각도 델타
        // 부호는 센서 부착 방향에 따라 조정 필요
        yaw += -rawGyZ * rotationSpeed * Time.deltaTime; 

        // [Pitch - 상하]: X축 회전. GyX 또는 GyY를 사용
        // 상하 움직임에는 GyX를 사용한다고 가정
        // Pitch는 Clamp가 필요하므로 적분 방식으로 적용
        pitch += -rawGyX * rotationSpeed * Time.deltaTime; 
        
        // [Roll - 기울기]: Z축 회전 (Roll은 사용하지 않거나, GyY를 사용)
        // roll은 그대로 0f를 유지합니다.

        // --- 3. Pitch 각도 제한 (목 꺾임 방지) ---
        pitch = Mathf.Clamp(pitch, -80f, 80f); 
        
        // --- 4. 가속도 데이터 통합 (필터링 - Pitch만) ---
        
        // 가속도 데이터 읽기 (절대 각도 계산용)
        float accY = serialManager.AcY;
        float accZ = serialManager.AcZ;
        
        // 가속도 센서를 이용한 Target Pitch (절대 각도) 계산
        // 센서 방향에 따라 accY, accZ 순서나 부호가 바뀔 수 있음
        // atan2(Y축 힘, Z축 힘) -> X축 회전 각도
        float accelTargetPitch = Mathf.Atan2(accY, accZ) * Mathf.Rad2Deg;
        
        // 상보 필터(Complementary Filter) 또는 가중치 평균을 사용하여 자이로 적분 값과 가속도 절대각을 섞습니다.
        // 현재 로직: pitch (자이로 적분 값)을 accelTargetPitch (가속도 값) 쪽으로 LerpAngle (부드럽게 보정)
        pitch = Mathf.LerpAngle(pitch, accelTargetPitch, 0.1f); // 0.1f는 반응 속도
        
        if (showDebugLog)
        {
            Debug.Log($"[Sensor] Pitch: {pitch:F1}, Yaw: {yaw:F1}");
        }
    }
    
    // --- 마우스/키보드 및 센서 입력 모두 이 함수를 사용합니다. ---
    /// <summary>
    /// 마우스 또는 센서 입력으로부터 받은 회전 델타를 누적 변수에 적용합니다.
    /// </summary>
    /// <param name="eulerAngles">회전 델타 (X: Pitch, Y: Yaw)</param>
    public void Rotate(Vector3 euler)
    {
        if (useSensor)
        {
            // 센서 모드에서는 마우스 입력이 들어오지 않으므로, 이 부분을 신경쓰지 않아도 됩니다.
            // 하지만 만약 센서와 마우스 입력을 동시에 받아 처리해야 한다면 로직 조정이 필요합니다.
            // 현재 요구사항은 "둘 다 지원"이므로, 입력 타입에 따라 덮어쓰지 않고 누적합니다.
            // 센서 모드에서는 ProcessSensorInput()이 yaw/pitch를 덮어씁니다.
            return; 
        }

        // 마우스/키보드 입력 모드 (Yaw와 Pitch 누적)
        // euler.y (Mouse X) -> Yaw (좌우)
        // euler.x (Mouse Y) -> Pitch (상하)
        
        // 마우스 감도(mouseRotationSpeed)를 적용하여 회전 속도 조절
        float sensitivity = mouseRotationSpeed * Time.deltaTime;
        
        yaw += euler.y * sensitivity;
        pitch += euler.x * sensitivity;
        
        // pitch 제한 (시야가 360도 회전하는 것을 방지)
        pitch = Mathf.Clamp(pitch, -80f, 80f); 

        if (showDebugLog)
        {
            Debug.Log($"[Mouse] Pitch: {pitch:F1}, Yaw: {yaw:F1}");
        }
    }
    
    /// <summary>
    /// 누적된 yaw/pitch 값을 사용하여 카메라에 최종 회전을 적용합니다.
    /// </summary>
    private void ApplyRotation()
    {
        // 4. 최종 회전 적용
        // Roll(Z축)은 항상 0으로 입력됨
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, roll);
        
        // 부드럽게 회전 (SmoothDamp와 유사한 Slerp 방식)
        // smoothTime 값이 클수록 회전이 느려집니다.
        cam.transform.localRotation = Quaternion.Slerp(cam.transform.localRotation, targetRotation, smoothTime * 10 * Time.deltaTime);
    }
}