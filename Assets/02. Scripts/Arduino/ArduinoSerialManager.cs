using UnityEngine;
using System.IO.Ports;

public class ArduinoSerialManager : MonoBehaviour
{
    public static ArduinoSerialManager Instance { get; private set; }

    [Header("Serial Settings")]
    public string portName = "/dev/cu.usbserial-1110";   // 맥 포트 이름
    public int baudRate = 9600;

    private SerialPort port;

    public bool IsConnected => port != null && port.IsOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        port = new SerialPort(portName, baudRate);
        port.ReadTimeout = 50;
        port.NewLine = "\n";

        try
        {
            port.Open();
            port.DtrEnable = true;
            port.RtsEnable = true;

            Debug.Log($"[ArduinoSerialManager] Opened {portName} @ {baudRate}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ArduinoSerialManager] Failed to open port: " + e.Message);
        }
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
}
