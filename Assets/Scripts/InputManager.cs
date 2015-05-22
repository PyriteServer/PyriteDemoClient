namespace PyriteDemoClient
{
    using UnityEngine;
	using UnityStandardAssets.CrossPlatformInput;

    public class InputManager : MonoBehaviour
    {
        public float RotationDeltaRate = 90;
        public float TranslationDeltaRate = 50.0f;
        public float TouchTranslationDeltaRate = 0.2f;

        private float _camPitch = 30;
        private float _yaw;
        private float _moveX;
        private float _moveY;
        private float _moveZ;
        private Quaternion _cameraOrientation;
        private Quaternion _rigOrientation;
        private Camera _pivotCamera;
        private Transform _targetPosition;

        private void Start()
        {
            _pivotCamera = GetComponentInChildren<Camera>();
            _targetPosition = transform;
        }

        private void FixedUpdate()
        {
           
            _moveX = CrossPlatformInputManager.GetAxis("Horizontal")*Time.deltaTime*TranslationDeltaRate;
            _moveY = CrossPlatformInputManager.GetAxis("Vertical")*Time.deltaTime*TranslationDeltaRate;
            _moveZ = CrossPlatformInputManager.GetAxis("Forward")*Time.deltaTime*TranslationDeltaRate;

            _yaw += CrossPlatformInputManager.GetAxis("HorizontalTurn")*Time.deltaTime*RotationDeltaRate;
            _camPitch += CrossPlatformInputManager.GetAxis("VerticalTurn")*Time.deltaTime*RotationDeltaRate;

            if (CrossPlatformInputManager.GetButton("XboxLB"))
            {
                _moveY -= Time.deltaTime*TranslationDeltaRate;
            }
            if (CrossPlatformInputManager.GetButton("XboxRB"))
            {
                _moveY += Time.deltaTime*TranslationDeltaRate;
            }


            _targetPosition.Translate(Vector3.up*_moveY, Space.World);
            _targetPosition.Translate(Vector3.forward*_moveZ + Vector3.right*_moveX, Space.Self);
        

            transform.position = Vector3.Lerp(transform.position, _targetPosition.position, Time.time);

            _rigOrientation.eulerAngles = new Vector3(0, LimitAngles(_yaw), 0);
            transform.rotation = Quaternion.Lerp(transform.rotation, _rigOrientation, Time.time);
            _cameraOrientation.eulerAngles = new Vector3(LimitAngles(_camPitch), transform.rotation.eulerAngles.y, 0);
            _pivotCamera.transform.rotation = Quaternion.Lerp(_pivotCamera.transform.rotation, _cameraOrientation,
                Time.time);

            var planePoint = transform.position;
            planePoint.y = 0;
            Debug.DrawLine(transform.position, planePoint, Color.green, 0f, true);
        }

        private static float LimitAngles(float angle)
        {
            var result = angle;

            while (result > 360)
                result -= 360;

            while (result < 0)
                result += 360;

            return result;
        }
    }
}