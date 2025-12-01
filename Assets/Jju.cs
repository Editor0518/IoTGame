using System;
using UnityEngine;

public class Jju : MonoBehaviour
{
    public bool move=false;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }
    
}
