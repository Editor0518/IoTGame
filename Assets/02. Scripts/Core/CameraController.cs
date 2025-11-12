using UnityEngine;

public class CameraController : MonoBehaviour
{
    /// <summary>
    /// 시점 회전을 담당합니다.
    /// KeyboardInput 또는 SensorInputProcessor에서 받은 회전 데이터를 받아 카메라를 돌립니다.
    /// </summary>
    /// 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
    Camera cam;
    
    void Start()
    {
        cam = Camera.main;
    }

    public void Rotate(Vector3 eulerAngles)
    {
        cam.transform.eulerAngles += eulerAngles;
    }
    
    void Update()
    {
        
    }
}
