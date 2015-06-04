using UnityEngine;
using System.Collections;

public class Rotator : MonoBehaviour
{
    public float TurnRate = 90f;


    void Update()
    {
        transform.Rotate(Vector3.up * Time.deltaTime * TurnRate);
    }
}
