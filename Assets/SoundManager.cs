using System;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;
    
    AudioSource audioSource;
    public AudioSource audioLoop;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlaySound(AudioClip clip)
    {
        if(clip==null)return;
        audioSource.PlayOneShot(clip);
    }

    public void PlaySoundLoop(AudioClip clip)
    {
        if(clip==null)return;
        audioLoop.Stop();
        audioLoop.clip = clip;
        audioLoop.Play();
    }

    public void StopSoundLoop()
    {
        audioLoop.Stop();
    }
}
