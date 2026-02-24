using UnityEngine;
using Polyperfect.Common; // 네임스페이스 추가 필수

public class Gun : MonoBehaviour
{
    [Header("Settings")]
    public AudioClip fireSound;
    public float range = 50f;

    public void Fire(Camera playerCam)
    {
        // 1. 소리 재생
        if (fireSound != null)
            SoundManager.instance.PlaySound(fireSound);

        // 2. 화면 중앙에서 레이 발사
        Ray ray = playerCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            // 3. 맞은 물체나 그 부모에게서 Common_WanderScript 찾기
            Common_WanderScript npcScript = hit.collider.GetComponentInParent<Common_WanderScript>();
            
            if (npcScript != null)
            {
                // 4. Die() 메서드 실행 (애니메이션 및 AI 중지 처리)
                npcScript.Die(); 
                Debug.Log($"[Gun] {hit.collider.name}을(를) 처치했습니다!");
            }
        }
    }
}