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
    public float AcX;
    public float AcY;
    public float AcZ;
    public float GyX;
    public float GyY; // 추가됨
    public float GyZ; // 추가됨

    private SerialPort serialPort;
    private Thread readThread;
    private bool isRunning = false;
    private string incomingData = "";


    private void Awake()
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
            port.WriteLine(message);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArduinoSerialManager] Write error: " + e.Message);
        }
    }

    public void SendRemainingTime(int seconds)
    {
        if (seconds < 0) seconds = 0;
        if (seconds > 9999) seconds = 9999;
        SendLine(seconds.ToString());
    }

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
        Instance = this;
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

}