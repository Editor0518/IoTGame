using System;
using UnityEngine;
using System.IO.Ports;
using System.Threading;
using TMPro;

public class ArduinoSerialManager : MonoBehaviour
{
    public static ArduinoSerialManager Instance { get; private set; }

    // 본인의 환경에 맞는 포트 이름으로 변경하거나 Inspector에서 설정하세요.
    [Header("Port Settings")]
    private string[] portNameList = { "/dev/cu.usbserial-1110", "COM3", "COM4" };

    [System.Serializable]
    public enum PortNum
    {
        MacOS, COM3, COM4
    }

    [Header("Serial Settings")]
    public PortNum portNum;   
    public int baudRate = 9600;
   
    private SerialPort port;
    public bool IsConnected => port != null && port.IsOpen;
    
    public TMP_Text debugText;

    [Header("Sensor Data (Read Only)")]
    public float GyX;
    public float GyY;
    public float GyZ;
    public float AcX;
    public float AcY;
    public float AcZ;
    
    [Header("Status Data (Read Only)")]
    public bool isButtonReleased = false; 
    public int arduinoTimerSeconds = -1; 

    private Thread readThread;
    private volatile bool isRunning = false; // 스레드 제어 플래그
    private string incomingData = ""; 

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // InputManager 설정을 확인하여 센서 모드일 때만 연결 시도
        if(InputManager.currentInputType.Equals(InputManager.InputType.SENSOR))
            OpenConnection();
    }

    public void OpenConnection()
    {
        if (IsConnected) return;

        // 포트 이름 설정
        string selectedPort = portNameList[(int)portNum];
        port = new SerialPort(selectedPort, baudRate);
        port.ReadTimeout = 100; // 타임아웃을 약간 넉넉하게 (50 -> 100)
        port.NewLine = "\n";

        try
        {
            port.Open();
            port.DtrEnable = true; // 아두이노 리셋 트리거 (필요시 true)
            port.RtsEnable = true;

            Debug.Log($"[ArduinoSerialManager] Opened {selectedPort} @ {baudRate}");

            // ⭐️ [핵심 수정] 데이터를 읽는 스레드를 여기서 시작해야 합니다.
            isRunning = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.Start();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ArduinoSerialManager] Failed to open port {selectedPort}: {e.Message}");
        }
    }
    
    public void CloseConnection()
    {
        isRunning = false;
        
        // 스레드가 종료될 때까지 잠시 대기
        if (readThread != null && readThread.IsAlive)
            readThread.Join(500); 

        if (port != null && port.IsOpen)
        {
            port.Close();
            port = null;
        }
        Debug.Log("[ArduinoSerialManager] Port Closed");
    }

    // 별도 스레드에서 계속 데이터를 읽음
    private void ReadSerialLoop()
    {
        while (isRunning && port != null && port.IsOpen)
        {
            try
            {
                // 데이터 읽기 (Timeout 발생 가능)
                string line = port.ReadLine();
                
                if (!string.IsNullOrEmpty(line))
                {
                    // 파싱은 데이터 정합성을 위해 스레드 내부에서 수행하거나,
                    // Unity 메인 스레드로 넘겨서 수행할 수 있습니다. 
                    // 여기서는 즉시 변수에 반영합니다.
                    ParseData(line);
                    
                    // 디버그용 텍스트 저장은 메인 스레드가 가져가도록 변수에 담음
                    incomingData = line; 
                }
            }
            catch (System.TimeoutException)
            {
                // 타임아웃은 데이터가 안 들어올 때 발생하는 자연스러운 현상이므로 무시
            }
            catch (System.Exception e)
            {
                // 디버그 로그는 스레드에서 직접 찍으면 에러가 날 수 있으나, 최신 유니티는 허용하기도 함.
                // 안전을 위해 catch만 해둡니다.
                // Debug.LogWarning("Read Error: " + e.Message);
            }
        }
    }

    private void Update()
    {
        // 들어온 데이터가 있으면 디버그 텍스트 갱신 (메인 스레드 처리)
        if (IsConnected && !string.IsNullOrEmpty(incomingData))
        {
            if(debugText != null) debugText.text = incomingData;
            // incomingData = ""; // 텍스트 깜빡임 방지를 위해 지우지 않고 덮어쓰기만 함
        }
    }

    private void ParseData(string line)
    {
        // 1. 초기화: 버튼 상태는 매 프레임(데이터 패킷마다) 리셋
        // 주의: 스레드에서 실행되므로 Unity API(Transform 등) 접근 금지
        isButtonReleased = false;

        try
        {
            // 형식: GYRO_X,GYRO_Y... | BUTTON:STATUS | TIME:MM:SS
            string[] blocks = line.Split('|');

            if (blocks.Length >= 3)
            {
                // [BLOCK 1] 센서 데이터
                string[] sensorData = blocks[0].Split(',');
                if (sensorData.Length >= 6)
                {
                    GyX = float.Parse(sensorData[0].Trim());
                    GyY = float.Parse(sensorData[1].Trim());
                    GyZ = float.Parse(sensorData[2].Trim());
                    AcX = float.Parse(sensorData[3].Trim());
                    AcY = float.Parse(sensorData[4].Trim());
                    AcZ = float.Parse(sensorData[5].Trim());
                }

                // [BLOCK 2] 버튼 상태
                if (blocks[1].Contains("BUTTON:"))
                {
                    string status = blocks[1].Split(':')[1].Trim();
                    isButtonReleased = status.Equals("RELEASED");
                }

                // [BLOCK 3] 타이머
                if (blocks[2].Contains("TIME:"))
                {
                    string timeStr = blocks[2].Split(':')[1].Trim();
                    string[] timeParts = timeStr.Split(':');
                    if (timeParts.Length == 2)
                    {
                        int minutes = int.Parse(timeParts[0].Trim());
                        int seconds = int.Parse(timeParts[1].Trim());
                        arduinoTimerSeconds = minutes * 60 + seconds;
                    }
                }
            }
        }
        catch (System.Exception)
        {
             // 파싱 에러 무시 (데이터 깨짐 등)
        }
    }

    public void SendLine(string message)
    {
        if (!IsConnected) return;
        try
        {
            port.WriteLine(message);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ArduinoSerialManager] Send Error: {e.Message}");
        }
    }

    public void SendRemainingTime(int seconds)
    {
        if (seconds < 0) seconds = 0;
        SendLine(seconds.ToString());
    }

    private void OnDestroy()
    {
        CloseConnection();
    }
}