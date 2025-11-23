using UnityEngine;
using System.IO.Ports;
using System.Threading;
using System;
using TMPro;

public class ArduinoSerialManager : MonoBehaviour
{
    /// <summary>
    /// 아두이노와의 시리얼 통신을 관리하고 데이터를 파싱하는 클래스입니다.
    /// </summary>
    public TMP_Text debugText;
    
    [Header("Serial Settings")]
    public string portName = "COM3"; // 아두이노 포트에 맞게 수정하세요 (예: COM3, /dev/tty.usb...)
    public int baudRate = 9600;

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

    void Start()
    {
        OpenConnection();
    }

    void OnDestroy()
    {
        CloseConnection();
    }

    public void OpenConnection()
    {
        if (serialPort != null && serialPort.IsOpen)
            return;

        try
        {
            serialPort = new SerialPort(portName, baudRate);
            serialPort.ReadTimeout = 100; // 타임아웃 설정
            serialPort.Open();
            
            isRunning = true;
            readThread = new Thread(ReadSerialLoop);
            readThread.Start();
            
            Debug.Log($"시리얼 포트 {portName} 연결 성공");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"시리얼 포트 연결 실패: {e.Message}\nPlayer Settings에서 API Compatibility Level이 .NET 4.x인지 확인하세요.");
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