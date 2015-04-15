using UnityEngine;
using System.Collections;

public class InputManager : MonoBehaviour
{
    public float RotationDeltaRate = 90;
    public float TranslationDeltaRate = 50.0f;

    private float camPitch = 30;
    private float yaw = 0;
    float move_x;
    float move_y;
    float move_z;
    private Quaternion cameraOrientation;
    private Quaternion rigOrientation;
    private Camera pivotCamera;
    private Transform targetPosition;

    void Start()
    {
        pivotCamera = GetComponentInChildren<Camera>();
        targetPosition = transform;
    }

    void Update()
    {        
        move_x = Input.GetAxis("Horizontal") * Time.deltaTime * TranslationDeltaRate;
        move_y = Input.GetAxis("Vertical") * Time.deltaTime * TranslationDeltaRate;
        move_z = Input.GetAxis("Forward") * Time.deltaTime * TranslationDeltaRate;

        yaw += Input.GetAxis("HorizontalTurn") * Time.deltaTime * RotationDeltaRate;
        camPitch += Input.GetAxis("VerticalTurn") * Time.deltaTime * RotationDeltaRate;

        if (Input.GetButton("XboxLB"))
        {
            move_y -= Time.deltaTime * TranslationDeltaRate;
        }
        if (Input.GetButton("XboxRB"))
        {
            move_y += Time.deltaTime * TranslationDeltaRate;
        }


        targetPosition.Translate(Vector3.up * move_y, Space.World);
        targetPosition.Translate(Vector3.forward * move_z + Vector3.right * move_x, Space.Self);                
    }

    void FixedUpdate()
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition.position, 1f);

        rigOrientation.eulerAngles = new Vector3(0, LimitAngles(yaw), 0);
        transform.rotation = Quaternion.Lerp(transform.rotation, rigOrientation, Time.time);
        cameraOrientation.eulerAngles = new Vector3(LimitAngles(camPitch), transform.rotation.eulerAngles.y, 0);
        pivotCamera.transform.rotation = Quaternion.Lerp(pivotCamera.transform.rotation, cameraOrientation, Time.time);

        var planePoint = transform.position;
        planePoint.y = 0;
        Debug.DrawLine(transform.position, planePoint, Color.green, 0f, true);
    }

    private static float LimitAngles(float angle)
    {
        float result = angle;

        while (result > 360)
            result -= 360;

        while (result < 0)
            result += 360;

        return result;
    }
}
