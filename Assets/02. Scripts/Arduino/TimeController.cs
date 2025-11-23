using UnityEngine;

public class TimerController : MonoBehaviour
{
    [Tooltip("타이머 총 시간(초). 5분 = 300초")]
    public int totalTimeSeconds = 300;  // 5분 = 300초

    private float remainingTime;
    private int lastSentSeconds = -1;
    private bool isRunning = false;
    private bool isTimeOver = false;

    void Start()
    {
        StartTimer();
    }

    /// <summary>
    /// 타이머를 totalTimeSeconds로 초기화하고 시작합니다.
    /// </summary>
    public void StartTimer()
    {
        remainingTime = totalTimeSeconds;
        isRunning = true;
        isTimeOver = false;
        lastSentSeconds = -1;

        // 시작 시 현재 시간(예: 300초) 한 번 보내두기
        SendTimeToArduino(Mathf.RoundToInt(remainingTime));
    }

    void Update()
    {
        if (!isRunning) return;

        // 1) 유니티 타임으로 카운트다운
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            isRunning = false;
            isTimeOver = true;
            Debug.Log("TIME OVER!");
        }

        int currentSeconds = Mathf.RoundToInt(remainingTime);

        // 2) 초 단위 값이 바뀔 때만 아두이노로 전송
        if (currentSeconds != lastSentSeconds)
        {
            lastSentSeconds = currentSeconds;
            SendTimeToArduino(currentSeconds);
        }
    }

    /// <summary>
    /// 남은 시간을 아두이노(7세그+부저)로 전송합니다.
    /// ArduinoSerialManager를 통해 시리얼로 초 단위 값이 보내집니다.
    /// </summary>
    /// <param name="seconds">현재 남은 시간(초)</param>
    private void SendTimeToArduino(int seconds)
    {
        if (ArduinoSerialManager.Instance == null)
        {
            Debug.LogWarning("[TimerController] ArduinoSerialManager.Instance가 없습니다.");
            return;
        }

        if (!ArduinoSerialManager.Instance.IsConnected)
        {
            Debug.LogWarning("[TimerController] 아두이노와 시리얼 연결이 열려있지 않습니다.");
            return;
        }

        // 디버그용으로 보고 싶으면 주석 해제
        Debug.Log("[TimerController] Send to Arduino: " + seconds);

        ArduinoSerialManager.Instance.SendRemainingTime(seconds);
    }
}
