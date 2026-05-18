using System;
using UnityEngine;

public class Walls : MonoBehaviour
{
    void Start()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider == null)
        {
            BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = false;
        }
        else
        {
            collider.isTrigger = false;
        }
    }

    void Update()
    {
        
    }
}