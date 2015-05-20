using UnityEngine;
using System.Collections;

public class InputManager : MonoBehaviour
{
    public float RotationDeltaRate = 90;
    public float TranslationDeltaRate = 50.0f;
	public float TouchTranslationDeltaRate = 0.2f;

    private float _camPitch = 30;
    private float _yaw = 0;
    float _moveX;
    float _moveY;
    float _moveZ;
    private Quaternion _cameraOrientation;
    private Quaternion _rigOrientation;
    private Camera _pivotCamera;
    private Transform _targetPosition;

    void Start()
    {
        _pivotCamera = GetComponentInChildren<Camera>();
        _targetPosition = transform;
    }    

    void FixedUpdate()
    {
        if (Input.touchCount > 0)
        {
			if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
			{

	            // Get movement of the finger since last frame
	            Vector2 touchDeltaPosition = Input.GetTouch(0).deltaPosition;


	            // Move object across XY plane
	            _targetPosition.Translate(
	                -touchDeltaPosition.x * TouchTranslationDeltaRate, 
					0,
	                -touchDeltaPosition.y * TouchTranslationDeltaRate);
			}
			// Pinch Zoom -> Y Axis
			if (Input.touchCount == 2)
			{
				// Store both touches.
				Touch touchZero = Input.GetTouch(0);
				Touch touchOne = Input.GetTouch(1);
				
				// Find the position in the previous frame of each touch.
				Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
				Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;
				
				// Find the magnitude of the vector (the distance) between the touches in each frame.
				float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
				float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;
				
				// Find the difference in the distances between each frame.
				float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;
				
				_targetPosition.Translate(
					0, 
					deltaMagnitudeDiff * TouchTranslationDeltaRate,
					0);
			}
        }
        else
        {
            _moveX = Input.GetAxis("Horizontal") * Time.deltaTime * TranslationDeltaRate;
            _moveY = Input.GetAxis("Vertical") * Time.deltaTime * TranslationDeltaRate;
            _moveZ = Input.GetAxis("Forward") * Time.deltaTime * TranslationDeltaRate;

            _yaw += Input.GetAxis("HorizontalTurn") * Time.deltaTime * RotationDeltaRate;
            _camPitch += Input.GetAxis("VerticalTurn") * Time.deltaTime * RotationDeltaRate;

            if (Input.GetButton("XboxLB"))
            {
                _moveY -= Time.deltaTime * TranslationDeltaRate;
            }
            if (Input.GetButton("XboxRB"))
            {
                _moveY += Time.deltaTime * TranslationDeltaRate;
            }


            _targetPosition.Translate(Vector3.up * _moveY, Space.World);
            _targetPosition.Translate(Vector3.forward * _moveZ + Vector3.right * _moveX, Space.Self);
        }

        transform.position = Vector3.Lerp(transform.position, _targetPosition.position, Time.time);

        _rigOrientation.eulerAngles = new Vector3(0, LimitAngles(_yaw), 0);
        transform.rotation = Quaternion.Lerp(transform.rotation, _rigOrientation, Time.time);
        _cameraOrientation.eulerAngles = new Vector3(LimitAngles(_camPitch), transform.rotation.eulerAngles.y, 0);
        _pivotCamera.transform.rotation = Quaternion.Lerp(_pivotCamera.transform.rotation, _cameraOrientation, Time.time);

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
