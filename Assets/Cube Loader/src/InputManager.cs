using UnityEngine;
using System.Collections;

public class InputManager : MonoBehaviour
{
    float RotationDeltaRate = 90;
    private float camPitch = 60;
    private float yaw = 0;
    float move_x;
    float move_y;
    float move_z;
    private Quaternion cameraOrientation;
    private Quaternion rigOrientation;
    private Camera pivotCamera;

    void Start()
    {
        pivotCamera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        move_x = Input.GetAxis("HorizontalMove");
        move_y = Input.GetAxis("VerticalMove");
        move_z = Input.GetAxis("ForwardMove");
        yaw += Input.GetAxis("Horizontal") * Time.deltaTime * RotationDeltaRate;
        camPitch += Input.GetAxis("Vertical") * Time.deltaTime * RotationDeltaRate;
    }

    void FixedUpdate()
    {
        transform.Translate(Vector3.up * move_y, Space.World);
        transform.Translate(Vector3.forward * move_z + Vector3.right * move_x, Space.Self);

        rigOrientation.eulerAngles = new Vector3(0, LimitAngles(yaw), 0);
        transform.rotation = Quaternion.Lerp(transform.rotation, rigOrientation, Time.time);
        cameraOrientation.eulerAngles = new Vector3(LimitAngles(camPitch), transform.rotation.eulerAngles.y, 0);
        pivotCamera.transform.rotation = Quaternion.Lerp(pivotCamera.transform.rotation, cameraOrientation, Time.time);

        var planePoint = transform.position;
        planePoint.y = 0;
        Debug.DrawLine(transform.position, planePoint, Color.green, 0f, true);
    }

    float LimitAngles(float angle)
    {
        float result = angle;

        while (result > 360)
            result -= 360;

        while (result < 0)
            result += 360;

        return result;
    }
}
