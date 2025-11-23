using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class InteractionHandler : MonoBehaviour
{
    /// <summary>
    /// PlayerObject
    /// 상호작용 가능한 오브젝트(벽, 문 등)를 감지하고,
    /// 상호작용 명령(키보드/센서)이 들어오면
    /// 해당 오브젝트의 InteractableObject 스크립트를 호출합니다.
    /// </summary>
    /// 
    public float distance = 0f;             // 드래그 시 카메라로부터의 거리

    private Camera cam;
    private Transform selectedObject;
    private Vector3 offset;
    private int layerMask;
    
    public enum InteractionType
    {
        JJU, STICK, SAUCE
    }

    void Start()
    {
        cam = Camera.main;
        layerMask = ~LayerMask.GetMask("NoRaycast");

    }

    public void PickUp(Transform obj, InteractionType type)
    {
        selectedObject = obj;
        selectedObject.localPosition = Vector3.zero;
        
    }

    public void PutDown()
    {
        selectedObject.parent = null;
    }

    public Outline outline;
    
    void SelectOutline()
    {
        
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        // Scene 뷰에서 레이가 보이게 그리기
        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red);
        
        // 레이캐스트로 오브젝트 맞았는지 체크
        if (Physics.Raycast(ray, out hit,100f, layerMask))
        {
            // 맞은 지점까지 선을 표시
            Debug.DrawLine(ray.origin, hit.point, Color.green);

            // 맞은 지점에 작은 표시점(구체) 찍기
            Debug.DrawRay(hit.point, Vector3.up * 0.2f, Color.yellow);
            
//            Debug.Log("ray hit obj's tag: "+hit.transform.tag);

            Outline newOutline=null;

            if (hit.transform.CompareTag("pot"))
            {
                if (hit.transform.parent != null)
                {

                    newOutline = hit.transform.parent.GetComponent<Outline>();
                }
            }
            else{
                
                newOutline = hit.transform.GetComponent<Outline>();
            
            }
            if (newOutline == null)
            {
                for (int i = 0; i < hit.transform.childCount; i++)
                {
                    newOutline = hit.transform.GetChild(i).GetComponent<Outline>();
                    if (newOutline != null)
                    {
                        break;
                    }
                }
            }
            if (newOutline != null)
            {
                if(outline!=null)
                    outline.enabled = false;
                newOutline.enabled = true;
                outline = newOutline;
            }
            else if(outline!=null)
            {
                outline.enabled = false;
            }
        }
        
       
    }
    
    void TrySelectObject()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 레이캐스트로 오브젝트 맞았는지 체크
        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            if (hit.collider.CompareTag("jju")||hit.collider.CompareTag("stick"))
            {
                selectedObject = hit.transform;

                // 클릭한 위치 기준 오프셋 계산 (튀지 않도록)
                Vector3 hitPoint = hit.point;
                offset = Vector3.zero;//selectedObject.position - hitPoint;

                // 물체까지의 거리 자동 설정 (필요하면 고정값도 OK)
                distance = Vector3.Distance(cam.transform.position, selectedObject.position);
                Debug.Log("hit jju/stick!");
            }
            else if (hit.collider.CompareTag("pot"))
            {
                //pot에서 새로운 jju를 인스턴스화 하기
                GetNewJjuFromPot();
                Debug.Log("hit pot!");
            }
            
            
        }
    }

    public GameObject jjuPrefab;
    public Transform pot;
    void GetNewJjuFromPot()
    {
        Debug.Log("get a jju from pot!");
        GameObject jju = Instantiate(jjuPrefab);
        jju.transform.position = pot.position;
        selectedObject = jju.transform;
        distance = Vector3.Distance(cam.transform.position, selectedObject.position);

        DragObject();
    }
    
    void DragObject()
    {
        // 화면 기준 포인트를 월드 좌표로 변환
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 point = ray.GetPoint(distance);

        // 오프셋 포함해 자연스럽게 이동시키기
        selectedObject.position = point + offset;
        selectedObject.GetComponent<Rigidbody>().isKinematic = true;
    }

    public void ActionHit()
    {
        Debug.Log("action hit!");
    }

    public void ActionOpenDoor()
    {
        Debug.Log("open door!");
    }

    
    void Update()
    {
        SelectOutline();
        // 마우스 왼쪽 버튼을 누른 순간 - 물체 선택
        if (Input.GetMouseButtonDown(0))
        {
            TrySelectObject();
        }

        // 드래그 중 - 선택한 오브젝트 이동
        if (Input.GetMouseButton(0) && selectedObject != null)
        {
            DragObject();
        }

        // 마우스 버튼을 떼면 놓기
        if (Input.GetMouseButtonUp(0) && selectedObject != null)
        {
            selectedObject.GetComponent<Rigidbody>().isKinematic = false;

            selectedObject = null;
        }
    }
}
