using System;
using UnityEngine;

public class GriddleFire : MonoBehaviour
{
    public ParticleSystem fire;
    public AudioClip clipJjuGrill;

    private int jjuCount = 0;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("jju"))
        {
            Debug.Log("jju+ "+other.name);
            fire.gameObject.SetActive(true);
            fire.Play();
            other.GetComponent<Jju>().isOnFire = true;
            jjuCount++;
            PlaySound();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("jju"))
        {
            fire.gameObject.SetActive(false);
            other.GetComponent<Jju>().isOnFire = false;
            jjuCount--;
            PlaySound();
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("jju"))
        {
            fire.gameObject.SetActive(true);
            fire.Play();
            jjuCount++;
            other.gameObject.GetComponent<Jju>().isOnFire = true;
            PlaySound();
        }
    }
    
    private void OnCollisionExit(Collision other)
    {
        if (other.gameObject.CompareTag("jju"))
        {
            fire.gameObject.SetActive(true);
            fire.Play();
            jjuCount--;
            other.gameObject.GetComponent<Jju>().isOnFire = false;
            PlaySound();
        }
    }

    void PlaySound()
    {
        if(jjuCount>0)
            SoundManager.instance.PlaySoundLoop(clipJjuGrill);
        else
        {
            SoundManager.instance.StopSoundLoop();
        }
    }
}
