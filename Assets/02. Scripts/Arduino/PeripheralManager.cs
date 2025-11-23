using UnityEngine;

/// <summary>
/// 게임 상태 변화에 따라 ArduinoSerialManager의 함수를 호출하여
/// LCD 문자열, LED 켜짐/꺼짐, 부저 알림 등을 제어합니다.
/// (시리얼 프로토콜 예시는 아래 함수들 주석 참고)
/// </summary>
public class PeripheralManager : MonoBehaviour
{
    public static PeripheralManager Instance { get; private set; }

    private void Awake()
    {
        // 싱글톤 세팅
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #region 공통 유틸

    /// <summary>
    /// 아두이노로 한 줄 명령을 전송하는 헬퍼.
    /// ArduinoSerialManager가 연결되어 있을 때만 보냄.
    /// </summary>
    private void SendCommand(string command)
    {
        if (ArduinoSerialManager.Instance == null)
        {
            Debug.LogWarning("[PeripheralManager] ArduinoSerialManager.Instance 없음");
            return;
        }

        if (!ArduinoSerialManager.Instance.IsConnected)
        {
            Debug.LogWarning("[PeripheralManager] 시리얼 포트가 열려있지 않음");
            return;
        }

        //Debug.Log("[PeripheralManager] CMD >> " + command);
        ArduinoSerialManager.Instance.SendLine(command);
    }

    #endregion

    #region LCD 제어

    /// <summary>
    /// LCD 두 줄 텍스트를 설정합니다.
    /// 프로토콜 예: LCD:line1|line2
    /// </summary>
    public void SetLCD(string line1, string line2 = "")
    {
        // 줄바꿈이나 | 같은 건 아두이노 파싱 규칙에 맞게 조심해서 사용
        string safeLine1 = line1.Replace("\n", " ").Replace("|", " ");
        string safeLine2 = line2.Replace("\n", " ").Replace("|", " ");

        string cmd = $"LCD:{safeLine1}|{safeLine2}";
        SendCommand(cmd);
    }

    /// <summary>
    /// LCD를 지웁니다.
    /// 프로토콜 예: LCD:CLEAR
    /// </summary>
    public void ClearLCD()
    {
        SendCommand("LCD:CLEAR");
    }

    #endregion

    #region LED 제어

    /// <summary>
    /// 특정 퍼즐 LED를 켜거나 끕니다.
    /// index는 아두이노에서 약속한 LED 번호(0,1,2...).
    /// 프로토콜 예: LED:1:ON / LED:2:OFF
    /// </summary>
    public void SetPuzzleLED(int index, bool on)
    {
        string onOff = on ? "ON" : "OFF";
        string cmd = $"LED:{index}:{onOff}";
        SendCommand(cmd);
    }

    /// <summary>
    /// 모든 퍼즐 LED를 켜거나 끕니다.
    /// 프로토콜 예: LED:ALL:ON / LED:ALL:OFF
    /// </summary>
    public void SetAllPuzzleLEDs(bool on)
    {
        string onOff = on ? "ON" : "OFF";
        string cmd = $"LED:ALL:{onOff}";
        SendCommand(cmd);
    }

    #endregion

    #region 부저 제어

    /// <summary>
    /// 타임오버 알림음 재생 요청.
    /// 프로토콜 예: BUZZ:TIMEOVER
    /// (아두이노에서 해당 패턴을 구현)
    /// </summary>
    public void PlayTimeOverBeep()
    {
        SendCommand("BUZZ:TIMEOVER");
    }

    /// <summary>
    /// 퍼즐 성공 등 긍정적인 효과음.
    /// 프로토콜 예: BUZZ:SUCCESS
    /// </summary>
    public void PlaySuccessBeep()
    {
        SendCommand("BUZZ:SUCCESS");
    }

    /// <summary>
    /// 현재 재생 중인 부저를 중지.
    /// 프로토콜 예: BUZZ:STOP
    /// </summary>
    public void StopBuzzer()
    {
        SendCommand("BUZZ:STOP");
    }

    #endregion

    #region 타이머(선택적으로 여기서도 래핑 가능)

    /// <summary>
    /// 남은 시간(초)을 아두이노로 직접 보낼 때 사용.
    /// (지금은 TimerController가 ArduinoSerialManager를 직접 쓰고 있지만,
    ///  나중에 주변장치 제어를 한 군데로 모으고 싶으면 여기로 옮겨도 됨.)
    /// </summary>
    public void SendRemainingTimeToArduino(int seconds)
    {
        if (seconds < 0) seconds = 0;
        if (seconds > 9999) seconds = 9999;

        if (ArduinoSerialManager.Instance == null || !ArduinoSerialManager.Instance.IsConnected)
        {
            Debug.LogWarning("[PeripheralManager] 타이머 전송 실패 (시리얼 연결 없음)");
            return;
        }

        ArduinoSerialManager.Instance.SendRemainingTime(seconds);
    }

    #endregion
}
