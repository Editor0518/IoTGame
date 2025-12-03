using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq; // LINQ를 사용하여 컬렉션(handPos의 자식들) 관리를 쉽게 합니다.

public class InteractionHandler : MonoBehaviour
{
    /// <summary>
    /// PlayerObject
    /// 상호작용 가능한 오브젝트(벽, 문 등)를 감지하고,
    /// 상호작용 명령(키보드/센서)이 들어오면
    /// 해당 오브젝트의 InteractableObject 스크립트를 호출합니다.
    /// </summary>
    ///
    
    public static InteractionHandler instance;
    
    // 이 변수는 더 이상 드래그에 사용되지 않지만, 다른 곳에서 사용될 수 있으므로 유지합니다.
    public float distance = 0f;             // 드래그 시 카메라로부터의 거리

    private Camera cam;
    [Header("잡힌 오브젝트 (Hand)")]
    // private Transform selectedObject; // ⭐ [수정] 단일 오브젝트 변수 제거. handPos의 자식들로 대체됩니다.
    private Vector3 offset;
    private int layerMask;

    [Header("오브젝트를 잡을 위치")]
    public Transform handPos; // 플레이어/카메라 앞에 오브젝트를 붙일 위치 Transform

    public TMP_Text rayText;
    
    public enum InteractionType
    {
        JJU, STICK, SAUCE
    }

    // ⭐ [수정] 오브젝트를 잡은 상태인지 확인 (handPos의 자식 개수로 판단)
    public bool IsHoldingObject => handPos.childCount > 0;
    
    // ⭐ [추가] 현재 들고 있는 쮸의 개수
    public int HeldJjuCount => GetHeldJjus().Count;

    // ⭐ [추가] handPos의 자식 중 jju 태그를 가진 리스트를 반환합니다.
    private List<Transform> GetHeldJjus()
    {
        return handPos.Cast<Transform>().Where(t => t.CompareTag("jju")).ToList();
    }
    
    // ⭐ [추가] handPos의 자식 중 jju가 아닌 오브젝트(Stick/Sauce)를 반환합니다.
    private Transform GetHeldNonJjuObject()
    {
        return handPos.Cast<Transform>().FirstOrDefault(t => !t.CompareTag("jju"));
    }

    void Awake()
    {
        instance = this;
    }
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
    /// jju는 최대 3개까지 들 수 있으며, stick/sauce는 jju와 동시에 들 수 없습니다.
    /// </summary>
    public void PickUp(Transform obj)
    {
        // 1. 현재 들고 있는 비(非) jju 오브젝트 확인 (Stick/Sauce)
        Transform currentlyHeldNonJju = GetHeldNonJjuObject();
        
        // 2. 들려는 오브젝트의 태그 확인
        bool isJju = obj.CompareTag("jju");

        // --- PickUp 제약 로직 ---
        if (isJju)
        {
            // A. Jju를 들려는 경우: Stick/Sauce를 들고 있으면 안 됨
            if (currentlyHeldNonJju != null)
            {
                Debug.LogWarning("[Interaction] Stick 또는 Sauce를 들고 있을 때는 Jju를 추가로 잡을 수 없습니다.");
                return;
            }

            // Jju 개수 제한 확인
            if (HeldJjuCount >= 3)
            {
                Debug.LogWarning("[Interaction] Jju는 최대 3개까지 들 수 있습니다.");
                return;
            }
            
            Debug.Log($"[Interaction] {obj.name} 잡음 (현재 Jju 개수: {HeldJjuCount + 1})");
        }
        else // B. Non-Jju (Stick/Sauce)를 들려는 경우: Jju들을 들고 있으면 안 됨
        {
            if (IsHoldingObject) // 이미 무언가를 들고 있다면 (Jju들을 들고 있다면)
            {
                Debug.LogWarning($"[Interaction] Jju를 들고 있을 때는 {obj.name}를 잡을 수 없습니다. 먼저 내려놓으세요.");
                return;
            }
            Debug.Log($"[Interaction] {obj.name} 잡음 (단일 오브젝트)");
        }
        
        // PickUp 공통 처리
        if (obj.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // ⭐ [수정] selectedObject 대신 obj를 handPos의 자식으로 설정
        obj.transform.parent = handPos;
        obj.localPosition = Vector3.zero; // 손 위치에 정확히
        obj.localRotation = Quaternion.identity; // 회전 초기화
        
        if(obj.CompareTag("jju"))
            SoundManager.instance.PlaySound(clipJjuPick);
        else if(obj.CompareTag("stick"))
            SoundManager.instance.PlaySound(clipStickPick);
        else if(obj.CompareTag("sauce"))
            SoundManager.instance.PlaySound(clipSaucePick);
    }

    /// <summary>
    /// 오브젝트를 놓습니다. 가장 먼저 잡은 오브젝트(0번째 자식)부터 놓습니다.
    /// </summary>
    public void PutDown(RaycastHit hit)
    {
        // ⭐ [수정] selectedObject 대신 handPos의 자식 개수로 체크
        if (handPos.childCount == 0) return;
    
        // ⭐ [핵심] 가장 먼저 잡은 오브젝트(0번째 자식)를 내려놓습니다.
        Transform objectToDrop = handPos.GetChild(0);

        // 리지드바디 상태 임시 저장 및 JjuStick 처리 (Stick을 내려놓는 경우)
        JjuStick jjuStick = null;
        if (objectToDrop.TryGetComponent(out jjuStick))
        {
            // Stick의 Transform(objectToDrop)에서 JjuStick 컴포넌트를 찾습니다.
            jjuStick.SetAttachedJjusKinematic(true); 
        }

        // Sound
        if(objectToDrop.CompareTag("jju"))
            SoundManager.instance.PlaySound(clipJjuDown);
        else if(objectToDrop.CompareTag("stick"))
            SoundManager.instance.PlaySound(clipStickPick);
        else if(objectToDrop.CompareTag("sauce"))
            SoundManager.instance.PlaySound(clipSaucePick);
    
        // 1. 부모 해제 (handPos에서 분리)
        objectToDrop.parent = null;
    
        // 2. 높이 보정 계산
        Renderer renderer = objectToDrop.GetComponent<Renderer>() ?? objectToDrop.GetComponentInChildren<Renderer>();
        
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
    
        // ⭐ Kinematic 상태일 때 위치를 직접 설정합니다.
        objectToDrop.position = placementPosition;
        
        // 4. 물리 활성화
        if (objectToDrop.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = false;
            rb.MovePosition(placementPosition);
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 5. 자식 Jju의 Kinematic 상태 유지 (Stick에 붙어있도록)
        if (jjuStick != null)
        {
            jjuStick.SetAttachedJjusKinematic(true); 
        }

        if (objectToDrop.TryGetComponent<Collider>(out Collider col))
        {
            col.isTrigger = false;
        }

        Debug.Log($"[Interaction] {objectToDrop.name} 놓음 (위치: {placementPosition})");
    }
    
    /// <summary>
    /// 잡고 있는 jju 중 가장 먼저 잡은 Jju(0번째 자식)를 stick의 특정 attachmentPoint에 끼웁니다.
    /// </summary>
    /// <param name="attachmentPoint">stickholder 태그를 가진 Transform</param>
    public void AttachJjuToStick(Transform attachmentPoint)
    {
        List<Transform> heldJjus = GetHeldJjus();
        
        // 안전 확인: 잡고 있는 물체가 jju인지 확인합니다.
        if (heldJjus.Count == 0) 
        {
            Debug.LogWarning("[AttachJjuToStick] 잡고 있는 jju가 없습니다.");
            return;
        }

        // ⭐ [핵심] 가장 먼저 잡은 Jju (0번째 자식)를 사용
        Transform jjuToAttach = heldJjus[0];

        if (attachmentPoint.TryGetComponent(out JjuStick jjuStick))
        {
            Rigidbody jjuRb = jjuToAttach.GetComponent<Rigidbody>();
            // JjuStick 내부에서 Attach 성공 시, jjuToAttach는 handPos의 자식에서 분리됩니다.
            jjuStick.AttachJjuToStick(jjuRb); 
            Debug.Log($"[Interaction] {jjuToAttach.name}을 {attachmentPoint.name}에 끼웠습니다.");
        }
    }

    // ⭐ [수정] selectedObject 변수가 없어졌으므로 이 함수는 더 이상 필요하지 않습니다.
    public void NoSelectedObject()
    {
        // Pass
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
        if (Physics.Raycast(ray, out hit, 5f, layerMask))
        {
            // --- 1. 잡고 있는 오브젝트가 있다면, 놓기 또는 끼우기 또는 "추가로 잡기" ---
            if (IsHoldingObject)
            {
                // A. Jju를 stickholder(끼우는 지점)에 끼우려는 시도인지 확인
                // 들고 있는 쮸가 있어야 하고 (HeldJjuCount > 0), hit된 물체가 stickPos 태그를 가져야 합니다.
                if (HeldJjuCount > 0 && hit.collider.CompareTag("stickPos"))
                {
                    Debug.Log("쮸 꽃기 시도!");
                    // 가장 먼저 잡은 Jju (0번째 자식)를 끼웁니다.
                    AttachJjuToStick(hit.transform);
                    return;
                }
                
                // ⭐️ B. 일반적인 놓기 방지: stick 본체 위에 놓는 것을 방지
                if (handPos.childCount > 0 && handPos.GetChild(0).CompareTag("jju") && hit.collider.CompareTag("stick"))
                {
                    Debug.Log("[Interaction] Jju는 Stick 본체에 직접 놓을 수 없습니다. Socket에 조준하세요.");
                    return;
                }
                
                // C. Sauce 사용 시도 (현재 놓으려는 오브젝트(0번째 자식)가 sauce인 경우)
                if (handPos.childCount > 0 && handPos.GetChild(0).CompareTag("sauce") && (hit.collider.CompareTag("jju")||hit.collider.CompareTag("stickPos")))
                {
                    Debug.Log("소스 바르기 시도!");
                    SauceUse(hit.transform);
                    return;
                }

                // ⭐ [수정됨] D. 추가로 Jju 잡기 (Stacking)
                // 현재 Jju를 들고 있고, 바라보는 대상이 'jju'이거나 'pot'이라면 '내려놓기' 대신 '추가 잡기'를 시도합니다.
                if (HeldJjuCount > 0)
                {
                    if (hit.collider.CompareTag("jju"))
                    {
                        // 이미 쮸를 들고 있어도, 바닥에 있는 다른 쮸를 클릭하면 PickUp을 시도합니다.
                        PickUp(hit.transform); 
                        return; // 내려놓지 않고 리턴
                    }
                    else if (hit.collider.CompareTag("pot"))
                    {
                        // 팟을 클릭하면 새 쮸를 꺼내서 추가로 잡습니다.
                        GetNewJjuFromPot(hit.transform);
                        return; // 내려놓지 않고 리턴
                    }
                }
                
                // E. 그 외 모든 경우 (바닥, 기타 오브젝트) -> 내려놓기
                PutDown(hit);
                return;
            }
            
            // --- 2. 잡고 있는 오브젝트가 없다면, 잡거나 (PickUp) 생성 (Instantiate) ---
            if (hit.collider.CompareTag("jju") || hit.collider.CompareTag("stick")||hit.collider.CompareTag("sauce"))
            {
                // 이미 존재하는 jju/stick 잡기
                // PickUp 내부에서 3개 제한 및 동시 들기 금지 로직이 처리됩니다.
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
                GetNewStickFromHolder(hit.transform);
                Debug.Log("hit stickholder!");
            }
            
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

    void SauceUse(Transform obj)
    {
        Jju jju=obj.GetComponent<Jju>();
        if (jju == null)
        {
            if(obj.childCount>0)
                jju=obj.GetChild(0).GetComponent<Jju>();
            else return;//쮸 안 꽂혀있음!
        }
        if (jju != null){
            jju.AddSauce(0.3f);
             SoundManager.instance.PlaySound(clipSauceUse);
        }
        
    }
    

    public GameObject jjuPrefab;
    public GameObject stickPrefab;
    
    // Pot 위치는 hit.transform을 통해 전달받는 것이 더 유연합니다.
    void GetNewJjuFromPot(Transform potTransform)
    {
        Debug.Log("get a jju from pot!");
        GameObject jju = Instantiate(jjuPrefab);
        
        // potTransform 위치에 일단 생성
        jju.transform.position = potTransform.position;
        
        // 생성 후 바로 잡습니다. (PickUp에서 개수 제한 체크)
        PickUp(jju.transform);
    }

    void GetNewStickFromHolder(Transform holderTransform)
    {
        Debug.Log("get a stick from stickholder!");
        GameObject stick = Instantiate(stickPrefab);
        
        // holderTransform 위치에 일단 생성
        stick.transform.position = holderTransform.position;
        
        // 생성 후 바로 잡습니다. (PickUp에서 동시 들기 금지 체크)
        PickUp(stick.transform);
    }
    
    
    
    void Update()
    {
        SelectOutline();
        if (ArduinoSerialManager.Instance.isButtonReleased) { TryInteract(); }
    }

    
    [Header("SOUND EFFECTS")]
    public AudioClip clipJjuDown;//쮸 내려놓음

    public AudioClip clipJjuPick;//쮸 집어드는 소리
    public AudioClip clipStickPick;//꼬치 집어드는 소리
    public AudioClip clipSaucePick;//소스 집어드는 소리
    public AudioClip clipSauceUse;//소스 푹짬





}