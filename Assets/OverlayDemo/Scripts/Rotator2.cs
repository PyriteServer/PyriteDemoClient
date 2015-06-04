using UnityEngine;
using System.Collections;

public class Rotator2 : MonoBehaviour
{
    public float TurnRate = 90f;
    public Vector3 Rotation;

    void Update()
    {
        transform.Rotate(Rotation * Time.deltaTime * TurnRate);
    }
}
