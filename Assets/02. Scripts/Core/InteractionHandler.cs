using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InteractionHandler : MonoBehaviour
{
    /// <summary>
    /// PlayerObject
    /// 상호작용 가능한 오브젝트(벽, 문 등)를 감지하고,
    /// 상호작용 명령(키보드/센서)이 들어오면
    /// 해당 오브젝트의 InteractableObject 스크립트를 호출합니다.
    /// </summary>
    /// 
    
    // 이 변수는 더 이상 드래그에 사용되지 않지만, 다른 곳에서 사용될 수 있으므로 유지합니다.
    public float distance = 0f;             // 드래그 시 카메라로부터의 거리

    private Camera cam;
    [Header("잡힌 오브젝트 (Hand)")]
    private Transform selectedObject;
    private Vector3 offset;
    private int layerMask;

    [Header("오브젝트를 잡을 위치")]
    public Transform handPos; // 플레이어/카메라 앞에 오브젝트를 붙일 위치 Transform

    public TMP_Text rayText;
    
    public enum InteractionType
    {
        JJU, STICK, SAUCE
    }

    // 오브젝트를 잡은 상태인지 확인
    public bool IsHoldingObject => selectedObject != null;

    void Start()
    {
        cam = Camera.main;
        // Raycast 마스크 설정: "NoRaycast" 레이어를 제외한 모든 레이어와 충돌
        layerMask = ~LayerMask.GetMask("NoRaycast");

        // 마우스 커서를 중앙에 고정하고 숨깁니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// 오브젝트를 손(handPos) 위치에 붙잡습니다.
    /// </summary>
    public void PickUp(Transform obj)
    {
        // 리지드바디 제거 및 키네마틱 설정
        if (obj.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        selectedObject = obj;
        selectedObject.transform.parent=(handPos);
        selectedObject.localPosition = Vector3.zero; // 손 위치에 정확히
        selectedObject.localRotation = Quaternion.identity; // 회전 초기화 (원하는 회전으로 조정 가능)
        
        Debug.Log($"[Interaction] {obj.name} 잡음 (HandPos 자식)");
    }

    /// <summary>
    /// 오브젝트를 놓습니다. 레이캐스트가 닿은 위치에 놓습니다. (높이 보정 로직 포함)
    /// </summary>
    public void PutDown(RaycastHit hit)
    {
        if (selectedObject == null) return;
        
        // 1. 부모 해제
        selectedObject.parent = null;
        
        // 2. 높이 보정 계산
        Renderer renderer = selectedObject.GetComponent<Renderer>();
        if (renderer == null)
        {
            renderer = selectedObject.GetComponentInChildren<Renderer>();
        }
        
        float halfHeight = 0f;
        if (renderer != null)
        {
            halfHeight = renderer.bounds.extents.y;
        }
        else
        {
            Debug.LogWarning("[InteractionHandler] Renderer를 찾을 수 없어 높이 보정이 정확하지 않을 수 있습니다. (기본값 사용)");
        }
        
        // 3. 충돌 지점에서 halfHeight만큼 hit.normal 방향으로 올려서 이동
        Vector3 placementPosition = hit.point + hit.normal * halfHeight;
        
        selectedObject.position = placementPosition;
        
        // 4. 물리 활성화
        if (selectedObject.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            // 놓을 때 속도가 0이 되도록 설정 
            rb.linearVelocity = Vector3.zero; // linearVelocity 대신 velocity 사용 권장
            rb.angularVelocity = Vector3.zero;
        }

        Debug.Log($"[Interaction] {selectedObject.name} 놓음 (위치: {placementPosition})");
        selectedObject = null;
    }

    /// <summary>
    /// 잡고 있는 jju를 stick의 특정 attachmentPoint에 끼웁니다.
    /// </summary>
    /// <param name="attachmentPoint">stickholder 태그를 가진 Transform</param>
    public void AttachJjuToStick(Transform attachmentPoint)
    {
        // 안전 확인: 잡고 있는 물체가 jju인지 확인합니다.
        if (selectedObject == null || !selectedObject.CompareTag("jju")) 
        {
            Debug.LogWarning("[AttachJjuToStick] 선택된 오브젝트가 jju가 아니거나 없습니다.");
            return;
        }

        // ⭐️ 1. 부모 (Stick)의 물리적 움직임을 잠시 멈춥니다.
        // attachmentPoint의 부모(Stick)를 찾습니다.
        Rigidbody stickRb = attachmentPoint.parent?.GetComponent<Rigidbody>();
        bool wasStickKinematic = false;
        
        if (stickRb != null)
        {
            wasStickKinematic = stickRb.isKinematic;
            stickRb.isKinematic = true; // Stick 고정
            // 추가: 붙일 때 충격을 없애기 위해 속도도 0으로 초기화
            stickRb.linearVelocity = Vector3.zero;
            stickRb.angularVelocity = Vector3.zero;
        }

        // 2. Jju의 물리 비활성화 및 설정
        if (selectedObject.TryGetComponent(out Rigidbody jjuRb))
        {
            jjuRb.isKinematic = true;
            jjuRb.useGravity = false; // 중력 비활성화
            jjuRb.linearVelocity = Vector3.zero; // Jju의 속도 초기화
            jjuRb.angularVelocity = Vector3.zero;
        }

        // 3. 부모 설정: jju를 attachmentPoint의 자식으로 만듭니다.
        selectedObject.SetParent(attachmentPoint);

        // 4. 로컬 위치/회전 조정: attachmentPoint의 중심에 맞춥니다.
        selectedObject.localPosition = Vector3.zero;
        selectedObject.localRotation = Quaternion.identity;

        // ⭐️ 5. 물리 업데이트를 기다린 후, Stick의 물리 상태를 원래대로 복구 (선택적)
        // 자식화가 끝난 후 다음 프레임에 Stick을 다시 움직일 수 있도록 합니다.
        if (stickRb != null)
        {
            // 이 로직은 Coroutine이 필요합니다. 간단한 해결을 위해 즉시 복구 대신,
            // 다음 프레임에 복구되도록 StartCoroutine을 사용하는 것이 좋습니다.
            // 하지만 이 환경에서는 Coroutine 사용을 지양하고, Stick은 영구적으로 Kinematic으로 설정하는 것을 권장합니다.
            // (만약 Stick이 움직이는 물체라면, 이 부분을 Coroutine으로 구현해야 합니다.)

            // Stick이 움직여야 하는 오브젝트라면:
            // StartCoroutine(RestoreStickPhysics(stickRb, wasStickKinematic));
            
            // Stick이 영구적으로 고정되어야 하는 오브젝트라면, 이 코드는 필요 없습니다.
        }

        Debug.Log($"[Interaction] {selectedObject.name}을 {attachmentPoint.name}에 끼웠습니다. (태그: {attachmentPoint.tag})");

        // 6. 잡고 있는 오브젝트 해제
        selectedObject = null;
    }
    
    // ... (이하 생략)

    public Outline outline;

    /// <summary>
    /// 화면 중앙을 기준으로 Raycast를 쏴서 Outline을 선택합니다.
    /// </summary>
    void SelectOutline()
    {
        // 화면 중앙 좌표를 월드 레이로 변환
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;
        
        // Debug Draw (씬 뷰에서 확인용)
        Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red);

        // Raycast 체크
        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            // Debug Draw
            Debug.DrawLine(ray.origin, hit.point, Color.green);

            Transform hitTransform = hit.transform;
            Outline newOutline = null;
            rayText.text="layer:"+hitTransform.gameObject.layer+", tag:"+hitTransform.gameObject.tag+", name:"+hitTransform.gameObject.name;

            // pot, stickholder(소스 컨테이너)처럼 부모에 Outline이 있는 경우 처리
            if (hitTransform.CompareTag("pot") || hitTransform.CompareTag("stickholder"))
            {
                if (hitTransform.parent != null)
                {
                    newOutline = hitTransform.parent.GetComponent<Outline>();
                }
            }
            // stickholder(끼우는 부분)가 자식인 경우에도 아웃라인을 띄워야 하므로, 여기서는 hitTransform 자체의 Outline을 확인합니다.
            else
            {
                // 일반 오브젝트
                newOutline = hitTransform.GetComponent<Outline>();
            }

            // 자식에게 Outline이 있는 경우 처리 (예: 복합 오브젝트)
            if (newOutline == null)
            {
                newOutline = hitTransform.GetComponentInChildren<Outline>();
            }

            // 아웃라인 업데이트
            if (newOutline != null && newOutline != outline)
            {
                if(outline != null) outline.enabled = false;
                newOutline.enabled = true;
                outline = newOutline;
            }
            else if (newOutline == null && outline != null)
            {
                outline.enabled = false;
                outline = null;
            }
        }
        else // 아무것도 맞지 않았을 때
        {
            if (outline != null)
            {
                outline.enabled = false;
                outline = null;
            }
        }
    }
    
    // ... (생략)

    /// <summary>
    /// 상호작용 (클릭) 시도를 처리합니다.
    /// </summary>
    public void TryInteract()
    {
        // Raycast는 항상 화면 중앙 기준
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit hit;

        // 레이캐스트로 오브젝트 맞았는지 체크
        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            // --- 1. 잡고 있는 오브젝트가 있다면, 놓기 또는 끼우기 ---
            if (IsHoldingObject)
            {
                // A. Jju를 stickholder(끼우는 지점)에 끼우려는 시도인지 확인
                // 잡고 있는 물체가 jju여야 하고, hit된 물체가 stickholder 태그를 가져야 합니다.
                if (selectedObject.CompareTag("jju") && hit.collider.CompareTag("stickPos"))
                {
                    Debug.Log("쮸 꽃기 시도!");
                    AttachJjuToStick(hit.transform);
                    return;
                }
                
                // ⭐️ B. 일반적인 놓기: stick 본체 위에 놓는 것을 방지
                // 잡고 있는 물체가 jju인데, hit된 물체가 stick 본체라면 무시하고 놓지 않도록 합니다.
                if (selectedObject.CompareTag("jju") && hit.collider.CompareTag("stick"))
                {
                    Debug.Log("[Interaction] Jju는 Stick 본체에 직접 놓을 수 없습니다. Socket에 조준하세요.");
                    return;
                }
                
                // C. 그 외 모든 경우 (바닥, Pot, 기타 오브젝트)에 놓기
                PutDown(hit);
                return;
            }
            
            // --- 2. 잡고 있는 오브젝트가 없다면, 잡거나 (PickUp) 생성 (Instantiate) ---
            if (hit.collider.CompareTag("jju") || hit.collider.CompareTag("stick"))
            {
                // 이미 존재하는 jju/stick 잡기
                PickUp(hit.transform);
            }
            else if (hit.collider.CompareTag("pot"))
            {
                // pot에서 새로운 jju를 인스턴스화 하여 잡기
                GetNewJjuFromPot(hit.transform);
                Debug.Log("hit pot!");
            }
            else if (hit.collider.CompareTag("stickholder"))
            {
                // stickholder(소스 컨테이너)에서 새로운 stick을 인스턴스화 하여 잡기
                // 주의: "stickholder" 태그가 두 가지 용도로 사용됩니다.
                // jju를 잡고 있지 않으므로, 이 stickholder는 소스 컨테이너일 가능성이 높습니다.
                GetNewStickFromHolder(hit.transform);
                Debug.Log("hit stickholder!");
            }
            // TODO: 다른 상호작용 (문 열기, 스위치) 로직 추가
        }
        else
        {
            // 아무것도 맞지 않았는데 잡고 있는 경우 (허공에 놓기)
            if (IsHoldingObject)
            {
                // 허공에 놓을 경우 handPos 앞에서 distance만큼 떨어진 곳에 놓습니다.
                Vector3 releasePoint = cam.transform.position + cam.transform.forward * 1.5f; // 예시 1.5m 앞
                PutDown(new RaycastHit { point = releasePoint });
            }
        }
    }

    // ... (생략)


    public GameObject jjuPrefab;
    public GameObject stickPrefab;
    
    // Pot 위치는 hit.transform을 통해 전달받는 것이 더 유연합니다.
    void GetNewJjuFromPot(Transform potTransform)
    {
        Debug.Log("get a jju from pot!");
        GameObject jju = Instantiate(jjuPrefab);
        
        // potTransform 위치에 일단 생성
        jju.transform.position = potTransform.position;
        
        // 생성 후 바로 잡습니다.
        PickUp(jju.transform);
    }

    void GetNewStickFromHolder(Transform holderTransform)
    {
        Debug.Log("get a stick from stickholder!");
        GameObject stick = Instantiate(stickPrefab);
        
        // holderTransform 위치에 일단 생성
        stick.transform.position = holderTransform.position;
        
        // 생성 후 바로 잡습니다.
        PickUp(stick.transform);
    }
    
    
    
    void Update()
    {
        SelectOutline();
    }
}