using System;
using Unity.Netcode;
using UnityEngine;

public class Rotate : NetworkBehaviour
{
    private float _rotationSpeed = 10f; // degrees per second
    private void Update()
    {
        transform.Rotate(Vector3.forward, _rotationSpeed * Time.deltaTime);
    }

}