using System;
using System.Collections.Generic;
using UnityEngine;

public class JjuStick : MonoBehaviour
{
    public int jjuCount = 0;
    Rigidbody rb;
    Transform[] attachPoints=new Transform[3];
    

    private void Start()
    {
        rb = GetComponent<Rigidbody>();

        for (int i = 0; i < attachPoints.Length; i++)
        {
            attachPoints[i]=transform.GetChild(i+1);
            //0번째 자식(stick body) 제외 다 가져옴
        }
    }

    public void AttachJjuToStick(Rigidbody jjuRb)
    {
        jjuCount = RefreshCount(); //혹시나 기존 스틱에서 쮸가 빠진 경우.
        if (jjuRb == null)
        {
            Debug.LogError("AttachJjuToStick: juRb is null");
            return;
        }

        if (jjuCount >= 3)
        {
            //꽉참. 더이상 끼울 수 없음.
            return;
        }

        
        jjuCount++;
        SoundManager.instance.PlaySound(clipJjuToStick);
        
        rb.isKinematic = true; // Stick 고정
        // 추가: 붙일 때 충격을 없애기 위해 속도도 0으로 초기화
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        jjuRb.isKinematic = true;
        jjuRb.useGravity = false; // 중력 비활성화
        jjuRb.linearVelocity = Vector3.zero; // Jju의 속도 초기화
        jjuRb.angularVelocity = Vector3.zero;
        
        // 3. 부모 설정: jju를 attachmentPoint의 자식으로 만듭니다.
        jjuRb.transform.parent =attachPoints[jjuCount-1];

        // 4. 로컬 위치/회전 조정: attachmentPoint의 중심에 맞춥니다.
        jjuRb.transform.localPosition = Vector3.zero;
        jjuRb.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);;
        
        jjuRb.useGravity = false; // 중력 비활성화
        jjuRb.linearVelocity = Vector3.zero; // Jju의 속도 초기화
        jjuRb.angularVelocity = Vector3.zero;
        
        // ⭐ 핵심: 콜라이더 비활성화
        if (jjuRb.TryGetComponent(out Collider jjuCol))
        {
            jjuCol.isTrigger = true;
        }
        
        InteractionHandler.instance.NoSelectedObject(); //성공적 종료
    }

    int RefreshCount()
    {
        int newCount=0;
        for (int i = 0; i < attachPoints.Length; i++)
        {
            if (attachPoints[i].childCount > 0)
            {
                newCount++;
            }
        }
        return newCount;
    }
    
    
    // JjuStick.cs 파일에 추가
    /// <summary>
    /// 모든 붙어있는 jju들의 Rigidbody를 제어합니다.
    /// </summary>
    public void SetAttachedJjusKinematic(bool isKinematicValue)
    {
        // attachPoints에 있는 모든 자식 Rigidbody에 접근합니다.
        for (int i = 0; i < attachPoints.Length; i++)
        {
            if (attachPoints[i].childCount > 0)
            {
                Transform jjuTransform = attachPoints[i].GetChild(0);
                if (jjuTransform.TryGetComponent(out Rigidbody jjuRb))
                {
                    // jju의 물리 상태를 변경합니다.
                    jjuRb.isKinematic = isKinematicValue;
                
                    // jju가 Stick의 움직임에 종속되도록 중력을 다시 켜거나 끕니다.
                    // jju가 Stick에 붙어있는 동안에는 중력을 비활성화 상태로 유지해야 합니다.
                    jjuRb.useGravity = !isKinematicValue; // isKinematic=false(물리 켬)일 때만 중력 켬
                    
                    jjuRb.linearVelocity = Vector3.zero;
                    jjuRb.angularVelocity = Vector3.zero;
                }
            }
        }
    }
    
    // JjuStick.cs 파일에 추가
    /// <summary>
    /// 모든 붙어있는 jju들의 Collider를 활성화/비활성화합니다.
    /// </summary>
    public void SetAttachedJjusColliderEnabled(bool isEnabled)
    {
        for (int i = 0; i < attachPoints.Length; i++)
        {
            if (attachPoints[i].childCount > 0)
            {
                Transform jjuTransform = attachPoints[i].GetChild(0);
                if (jjuTransform.TryGetComponent(out Collider jjuCol))
                {
                    // Jju의 콜라이더를 켜거나 끕니다.
                    jjuCol.isTrigger = isEnabled; 
                }
            }
        }
    }

    public string[] GetJjuScore()
    {
        List<Jju> jjuList = new List<Jju>();
        for (int i = 0; i < attachPoints.Length; i++)
        {
            if(attachPoints[i].childCount > 0)
                jjuList.Add(attachPoints[i].GetChild(0).GetComponent<Jju>());
        }
        int[] score = {0,0,0};
        string msg1="",msg2="";
        
        for (int i = 0; i < jjuList.Count; i++)
        {
            float cookLevel = jjuList[i].cookLevel;
            float sauceAmount = jjuList[i].sauceAmount;

            int thisScore = 100; //개당 100캐피로 시작.

            
            if (cookLevel >= 0.9f) //0.9 ~ 1.0+ 타버림
            {
                //burnt!
                thisScore = -300;
                score[i] = thisScore;
                msg2 = "쮸가 다 타서 왔어요... ";
                break;
            }
            else if (cookLevel >= 0.55 && cookLevel <= 0.65f) //0.55 ~ 0.65 (0.1 구간)
            {
                //very well!
                thisScore = 150;
                if(msg2!="")
                    msg2="너무너무 맛있어요! ";
            }
            else if (cookLevel >= 0.4 && cookLevel < 0.55f) //0.4 ~ 0.54 (0.14 구간)
            {
                //good!
                thisScore = 100;
                if(msg2!="")
                    msg2="맛있네요! ";
            }
            else if (cookLevel < 0.4f) //0.0 ~ 0.54 (0.4 구간)
            {
                //not cooked!
                thisScore = 0;
                score[i] = thisScore;
                msg2="쮸가 안 익었어요... ";
                break;
            }
            else if (cookLevel < 0.9f) //0.66 ~ 0.89 (0.23 구간)
            {
                //overcooked!
                thisScore = 70;
                score[i] = thisScore;
                msg2="쮸가 너무 많이 익었어요... ";
                break;
            }
            //소스가 충분히 안발라져 있으면 50캐피 감점
            if (sauceAmount < 0.95f)
            {
                thisScore -= 50;
                msg1 = "소스가 덜 묻었고, ";
            }
            score[i] = thisScore;
        }
        int finalScore=score[0]+score[1]+score[2];
        if(finalScore<0) finalScore=0;
        
        return new string[] { msg1+msg2+finalScore.ToString()+"캐피 드리겠습니다.", finalScore.ToString() };
    }
    public AudioClip clipJjuToStick;//쮸 꼬치에 끼움
}
