using System;
using UnityEngine;
using System.IO.Ports;
using System.Threading;
using TMPro;

public class ArduinoSerialManager : MonoBehaviour
{
    public static ArduinoSerialManager Instance { get; private set; }

    private string[] portName = { "/dev/cu.usbserial-1110", "COM3", "COM4" };

    [System.Serializable]
    public enum PortNum
    {
        MacOS, COM3, COM4
    }

    [Header("Serial Settings")]
    public PortNum portNum;   // 맥 포트 이름
    public int baudRate = 9600;
   
    private SerialPort port;

    public bool IsConnected => port != null && port.IsOpen;
    
    
    public TMP_Text debugText;
    // 파싱된 센서 데이터 (외부에서 접근 가능)
    [Header("Sensor Data (Read Only)")]
    public float GyX;
    public float GyY;
    public float GyZ;
    public float AcX;
    public float AcY;
    public float AcZ;
    
    [Header("Status Data (Read Only)")]
    public bool isButtonReleased = false; // 새로운 버튼 상태 플래그
    public int arduinoTimerSeconds = -1;  // 아두이노에서 파싱된 시간 (초 단위)

    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;
    private string incomingData = ""; // 사용되지 않으므로 제거


    private void Awake()
    {
        Instance = this;
        
    }

    private void Start()
    {
        if(InputManager.currentInputType.Equals(InputManager.InputType.SENSOR))
            OpenConnection();
    }

    public void SendLine(string message)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("[ArduinoSerialManager] Port not open");
            return;
        }

        try
        {
           // Debug.Log("[ArduinoSerialManager] msg: "+message);
            port.WriteLine(message);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArduinoSerialManager] Write error: " + e.Message);
        }
    }
/*
    public void SendRemainingTime(int seconds)
    {
        if (seconds < 0) seconds = 0;
        if (seconds > 9999) seconds = 9999;
        SendLine(seconds.ToString());
    }*/

    private void OnDestroy()
    {
        if (port != null && port.IsOpen)
        {
            port.Close();
        }
    }
    
    public void OpenConnection()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        DontDestroyOnLoad(gameObject);

        port = new SerialPort(portName[(int)portNum], baudRate);
        port.ReadTimeout = 50;
        port.NewLine = "\n";

        try
        {
            port.Open();
            port.DtrEnable = true;
            port.RtsEnable = true;

            Debug.Log($"[ArduinoSerialManager] Opened {portName[(int)portNum]} @ {baudRate}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArduinoSerialManager] Failed to open port: " + e.Message);
        }
        
    }
    
    public void CloseConnection()
    {
        isRunning = false;
        
        if (readThread != null && readThread.IsAlive)
            readThread.Join();

        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            serialPort = null;
        }
    }
/*
    // 별도 스레드에서 계속 데이터를 읽음
    private void ReadSerialLoop()
    {
        while (isRunning && serialPort != null && serialPort.IsOpen)
        {
            try
            {
                string line = serialPort.ReadLine();
                debugText.text = line;
                if (!string.IsNullOrEmpty(line))
                {
                    ParseData(line);
                }
            }
            catch (System.Exception)
            {
                // 타임아웃 등 읽기 에러 무시
            }
        }
    }

    // 데이터 파싱 함수
    // 형식: "가속도 X: -14828 | Y: -2756 | Z: -5264 | Gyro X: -245 | Gyro Y: -130 | Gyro Z: 55"
    private void ParseData(string line)
    {
        try
        {
            // '|'를 기준으로 분리
            string[] parts = line.Split('|');

            if (parts.Length >= 6) // 데이터 개수가 6개로 늘어남
            {
                // 가속도 X 파싱 ("가속도 X: -14828")
                string acXStr = parts[0].Split(':')[1].Trim();
                AcX = float.Parse(acXStr);

                // 가속도 Y 파싱 (" Y: -2756")
                string acYStr = parts[1].Split(':')[1].Trim();
                AcY = float.Parse(acYStr);

                // 가속도 Z 파싱 (" Z: -5264")
                string acZStr = parts[2].Split(':')[1].Trim();
                AcZ = float.Parse(acZStr);

                // Gyro X 파싱 (" Gyro X: -245")
                string gyXStr = parts[3].Split(':')[1].Trim();
                GyX = float.Parse(gyXStr);

                // Gyro Y 파싱 (" Gyro Y: ...")
                string gyYStr = parts[4].Split(':')[1].Trim();
                GyY = float.Parse(gyYStr);

                // Gyro Z 파싱 (" Gyro Z: ...")
                string gyZStr = parts[5].Split(':')[1].Trim();
                GyZ = float.Parse(gyZStr);
            }
        }
        catch (System.Exception e)
        {
             // 파싱 에러 처리
        }
    }

*/

    // ... (Awake, SendLine, OnDestroy, OpenConnection, CloseConnection 등 기존 로직 유지)

    // 별도 스레드에서 계속 데이터를 읽음
    private void ReadSerialLoop()
    {
        while (isRunning && port != null && port.IsOpen) // serialPort 대신 port 사용
        {
            try
            {
                string line = port.ReadLine(); // serialPort 대신 port 사용
                //debugText.text = line; // 디버그 텍스트 업데이트는 Main Thread에서만 가능하므로 Update()로 이동해야 함
                incomingData = line;
                
                if (!string.IsNullOrEmpty(line))
                {
                    ParseData(line);
                }
            }
            catch (System.Exception)
            {
                // 타임아웃 등 읽기 에러 무시
            }
        }
    }

    // 메인 스레드에서 debugText 업데이트 (ReadSerialLoop에서 직접 접근 불가)
    private void Update()
    {
        if (isRunning && port != null && port.IsOpen && !string.IsNullOrEmpty(incomingData))
        {
            debugText.text = incomingData; // ReadSerialLoop에서 데이터를 업데이트해야 함
            ReadSerialLoop();
        }
    }

    // NOTE: SerialPort를 포트 연결에 사용한 'port' 변수로 통일하고, 스레드 로직을 개선해야 합니다.
    // 기존 코드의 문제: ReadSerialLoop에서 'serialPort'를 사용하는데, OpenConnection에서는 'port'를 사용하고 있습니다.
    // 'serialPort' 변수 선언을 제거하고 'port'를 사용하도록 수정합니다. (아래 코드는 이를 반영함)

    // 데이터 파싱 함수 - 새로운 포맷에 맞춰 변경
    private void ParseData(string line)
    {
        // 1. 초기화: 버튼 상태는 매 프레임 초기화
        isButtonReleased = false;

        try
        {
            // ' | '를 기준으로 분리: [센서 데이터], [버튼 상태], [시간]
            string[] blocks = line.Split('|');

            if (blocks.Length >= 3)
            {
                // 1. 센서 데이터 파싱 (쉼표로 구분)
                string[] sensorData = blocks[0].Split(',');
                if (sensorData.Length >= 6)
                {
                    // Gyro (X, Y, Z) - 첫 3개
                    GyX = float.Parse(sensorData[0].Trim());
                    GyY = float.Parse(sensorData[1].Trim());
                    GyZ = float.Parse(sensorData[2].Trim());
                    // Accel (X, Y, Z) - 다음 3개
                    AcX = float.Parse(sensorData[3].Trim());
                    AcY = float.Parse(sensorData[4].Trim());
                    AcZ = float.Parse(sensorData[5].Trim());
                }

                // 2. 버튼 상태 파싱 (BUTTON:STATUS)
                if (blocks[1].Contains("BUTTON:"))
                {
                    string status = blocks[1].Split(':')[1].Trim();
                    isButtonReleased = status.Equals("RELEASED");
                }

                // 3. 타이머 시간 파싱 (TIME:MM:SS)
                if (blocks[2].Contains("TIME:"))
                {
                    string timeStr = blocks[2].Split(':')[1].Trim(); // MM:SS 문자열
                    string[] timeParts = timeStr.Split(':');
                    if (timeParts.Length == 2)
                    {
                        int minutes = int.Parse(timeParts[0].Trim());
                        int seconds = int.Parse(timeParts[1].Trim());
                        arduinoTimerSeconds = minutes * 60 + seconds;
                    }
                }
                Debug.Log($"[ArduinoSerialManager] Received {line}");
            }
            else
            {
                // 만약 타이머 설정 명령 (TIMER_SET:300)과 같은 단독 메시지인 경우 처리
                if (line.StartsWith("TIMER_SET:"))
                {
                    // 아두이노에서 타이머가 재설정되었을 때의 피드백 메시지 처리 가능
                }
                // 디버그 메시지 등 다른 단독 메시지 처리 가능
            }
        }
        catch (System.Exception e)
        {
             // 파싱 에러 처리
             Debug.LogError($"[ArduinoSerialManager] 파싱 에러: {line} -> {e.Message}");
        }
    }

    // ... (기존 ReadSerialLoop, Update, OnDestroy 등 유지)

    // 기존 SendRemainingTime는 아두이노가 시간을 직접 처리하므로 이제 필요 없음.
    // 하지만 TimerController.cs에서 이 함수를 호출하고 있으므로, 함수 자체는 유지하되 로직을 변경합니다.
    public void SendRemainingTime(int seconds)
    {
        // 아두이노가 타이머를 관리하고, Unity는 시간을 파싱하게 되었으므로,
        // Unity에서 시간을 설정하는 경우에만 이 함수를 사용합니다.
        if (seconds < 0) seconds = 0;
        SendLine(seconds.ToString()); // 초 단위로만 전송
//        Debug.Log($"[ArduinoSerialManager] Time Set Command Sent: {seconds}");
    }
}


