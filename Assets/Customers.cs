using System;
using TMPro;
using UnityEngine;

public class Customers : MonoBehaviour
{
    public AudioClip clipCappy;
    public static int totalCappy = 0;
    public TMP_Text cappyText;
    public TMP_Text customerSay;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("stick"))
        {
           string[] msg = other.GetComponent<JjuStick>().GetJjuScore();
           totalCappy += int.Parse(msg[1]);
           cappyText.text = totalCappy.ToString();
           CustomerSay(msg[0]);
           SoundManager.instance.PlaySound(clipCappy);
           Debug.Log(msg);
           Destroy(other.gameObject, 0.5f);
        }
    }

    private void Start()
    {
        totalCappy = 0;
    }

    void CustomerSay(string msg)
    {
        customerSay.gameObject.SetActive(false);
        customerSay.text = msg;
        customerSay.gameObject.SetActive(true);
    }
}
