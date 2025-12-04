using System;
using System.Collections;
using System.Collections.Generic;
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
           ArduinoSerialManager.Instance.SendRevenue();
           CustomerSay(msg[0]);
           SoundManager.instance.PlaySound(clipCappy);
           Debug.Log(msg);
           Destroy(other.gameObject, 0.5f);
        }
    }

    private void Start()
    {
        totalCappy = 0;
       ArduinoSerialManager.Instance.SendRevenue();

       StartCoroutine(Test());
    }

    void CustomerSay(string msg)
    {
        customerSay.gameObject.SetActive(false);
        customerSay.text = msg;
        customerSay.gameObject.SetActive(true);
    }

    IEnumerator Test()
    {
        while (totalCappy<=2000)
        {
            totalCappy += 10;
            cappyText.text = totalCappy.ToString();
            ArduinoSerialManager.Instance.SendRevenue();
            yield return new WaitForSeconds(1f);
        }
    }
}
