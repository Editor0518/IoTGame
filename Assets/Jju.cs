using System;
using UnityEngine;

public class Jju : MonoBehaviour
{
    public bool move=false;
    private Rigidbody rb;
    Renderer rend;
    public Material burntMaterial;
    Material defaultMaterial;

    private Renderer eyeRend;
    Material eyeMaterial;
    
    public float cookLevel = 0f;
    public float sauceAmount = 0f;
    
    public bool isOnFire=false;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rend = transform.GetChild(1).GetComponent<Renderer>();
        defaultMaterial=rend.material;
        
        eyeRend = transform.GetChild(0).GetComponent<Renderer>();
        eyeMaterial=eyeRend.material;
        ResetCookLevel();
    }

    private void Update()
    {
        if(isOnFire)
            ChangeCookLevel(0.1f*Time.deltaTime);
    }

    void ChangeCookLevel(float add)
    {
        cookLevel += add;
        if (cookLevel > 1.1f) return;
        
        rend.material.SetFloat("_CookLevel", cookLevel);
        if (cookLevel >= 1f)
        {
            rend.material=burntMaterial;
            eyeRend.material=burntMaterial;
        }
    }

    void ResetCookLevel()
    {
        cookLevel = 0f;
        rend.material = defaultMaterial;
        rend.sharedMaterial.SetFloat("_CookLevel", 0f);
        rend.sharedMaterial.SetFloat("_SauceAmount", 0f);
        eyeRend.material = eyeMaterial;
        sauceAmount = 0f;
                
    }

    void ResetSauce()
    {
        
    }

    public void AddSauce(float add)
    {
        sauceAmount += add;
        rend.sharedMaterial.SetFloat("_SauceAmount", sauceAmount);
    }
    
    
}
