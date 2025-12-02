using System;
using UnityEngine;

public class GriddleFire : MonoBehaviour
{
    public ParticleSystem fire;
        
    void Start()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("jju"))
        {
            fire.gameObject.SetActive(true);
            fire.Play();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("jju"))
        {
            fire.gameObject.SetActive(false);
        }
    }
}
