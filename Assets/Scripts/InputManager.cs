namespace PyriteDemoClient
{
    using UnityEngine;
	using UnityStandardAssets.CrossPlatformInput;

    public class InputManager : MonoBehaviour
    {
        public float RotationDeltaRate = 20;
        public float TranslationDeltaRate = 50.0f;
        public float TouchTranslationDeltaRate = 0.2f;
        public bool InvertY = false;
		public GameObject altitudeIcon, moveIcon, orbitIcon;

        private float _camPitch = 30;
        private float _yaw;
        private float _moveX;
        private float _moveY;
        private float _moveZ;
		private Vector3 _lastMove;
		private Vector3 _thisMove;
        private Quaternion _cameraOrientation;
        private Camera _pivotCamera;
        private Transform _targetPosition;
		private float momentumStartTime;

        private void Update()
        {
           
			// Keyboard
			_moveX = Input.GetAxis("Horizontal")*Time.deltaTime*TranslationDeltaRate;
			_moveY = Input.GetAxis("Vertical")*Time.deltaTime*TranslationDeltaRate;
			_moveZ = Input.GetAxis("Forward")*Time.deltaTime*TranslationDeltaRate;

			_yaw += Input.GetAxis("HorizontalTurn")*Time.deltaTime*RotationDeltaRate;
			_camPitch -= Input.GetAxis("VerticalTurn")*Time.deltaTime*RotationDeltaRate * (InvertY ? -1f : 1f);

			// Touch
			if (Input.touchCount == 1) {
				_moveX = Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 1.5f;
				_moveZ = Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 1.5f;

				if (orbitIcon != null)
				{
					moveIcon.SetActive(true);
					orbitIcon.SetActive(false);
					altitudeIcon.SetActive(false);
				}
			}

			if (Input.touchCount == 3) {
				_yaw += Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 0.4f;
				_camPitch -= Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 0.4f;

				if (orbitIcon != null)
				{
					orbitIcon.SetActive(true);
					moveIcon.SetActive (false);
					altitudeIcon.SetActive(false);
				}
			}

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
				
				_moveY = deltaMagnitudeDiff * TouchTranslationDeltaRate;

				if (orbitIcon != null)
				{
					altitudeIcon.SetActive(true);
					orbitIcon.SetActive(false);
					moveIcon.SetActive (false);
				}
			}

			if (Input.touchCount == 0 && orbitIcon != null) {

				orbitIcon.SetActive(false);
				moveIcon.SetActive (false);
				altitudeIcon.SetActive(false);
			}

			// Limit movement
			_camPitch = Mathf.Clamp (_camPitch, -60, 80);

			if (Input.GetButton("XboxLB"))
            {
                _moveY -= Time.deltaTime*TranslationDeltaRate;
            }
			if (Input.GetButton("XboxRB"))
            {
                _moveY += Time.deltaTime*TranslationDeltaRate;
            }

			// Handle momentum
			if ((_moveX == 0 && _moveY == 0 && _moveZ == 0)) {
					if (momentumStartTime == 0)
					{
						momentumStartTime = Time.time;
					}
					float fraction = (Time.time - momentumStartTime) / 0.7f;
					var momentum = Vector3.Lerp (_lastMove, Vector3.zero, fraction);
					_thisMove = new Vector3(
						_moveX==0?momentum.x:_moveX,
						_moveY==0?momentum.y:_moveY,
						_moveZ==0?momentum.z:_moveZ);					

			} else {
				_lastMove = new Vector3 (_moveX, _moveY, _moveZ);
				_thisMove = _lastMove;
				momentumStartTime = 0;
			}		

			// Rotate
			_cameraOrientation.eulerAngles = new Vector3(LimitAngles(_camPitch), LimitAngles(_yaw), 0);
			transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation,
			                                     Time.time);

			// Translate
			var movementVector = _thisMove;
			var movementRotation = Quaternion.Euler(new Vector3(0f,transform.eulerAngles.y,0f));
			movementVector = movementRotation * movementVector;
			transform.Translate(movementVector, Space.World);


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