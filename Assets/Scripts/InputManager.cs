namespace PyriteDemoClient
{
    using UnityEngine;
	using UnityStandardAssets.CrossPlatformInput;

    public class InputManager : MonoBehaviour
    {
        public float RotationDeltaRate = 20;
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

        private void Update()
        {
           
            _moveX = CrossPlatformInputManager.GetAxis("Horizontal")*Time.deltaTime*TranslationDeltaRate;
            _moveY = CrossPlatformInputManager.GetAxis("Vertical")*Time.deltaTime*TranslationDeltaRate;
            _moveZ = CrossPlatformInputManager.GetAxis("Forward")*Time.deltaTime*TranslationDeltaRate;

            _yaw += CrossPlatformInputManager.GetAxis("HorizontalTurn")*Time.deltaTime*RotationDeltaRate;
            _camPitch -= CrossPlatformInputManager.GetAxis("VerticalTurn")*Time.deltaTime*RotationDeltaRate;


            if (CrossPlatformInputManager.GetButton("XboxLB"))
            {
                _moveY -= Time.deltaTime*TranslationDeltaRate;
            }
            if (CrossPlatformInputManager.GetButton("XboxRB"))
            {
                _moveY += Time.deltaTime*TranslationDeltaRate;
            }

			// Rotate
			_cameraOrientation.eulerAngles = new Vector3(LimitAngles(_camPitch), LimitAngles(_yaw), 0);
			transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation,
			                                                  Time.time);

			// Translate
			var movementVector = new Vector3 (_moveX, _moveY) + (_moveZ * Vector3.forward);
			//movementVector = (Quaternion.Inverse(_pivotCamera.transform.rotation)) * movementVector;
			transform.Translate(movementVector, Space.Self);

            // var planePoint = transform.position;
            // planePoint.y = 0;
            // Debug.DrawLine(transform.position, planePoint, Color.green, 0f, true);
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