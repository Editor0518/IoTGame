using UnityEngine;

public class InteractionHandler : MonoBehaviour
{
    /// <summary>
    /// PlayerObject
    /// 상호작용 가능한 오브젝트(벽, 문 등)를 감지하고,
    /// 상호작용 명령(키보드/센서)이 들어오면
    /// 해당 오브젝트의 InteractableObject 스크립트를 호출합니다.
    /// </summary>
    
    void Start()
    {
        
    }

    public void ActionHit()
    {
        Debug.Log("hit!");
    }

    public void ActionOpenDoor()
    {
        Debug.Log("open door!");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
