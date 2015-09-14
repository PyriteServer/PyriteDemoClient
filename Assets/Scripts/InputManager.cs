namespace PyriteDemoClient
{
    using UnityEngine;

    public class InputManager : MonoBehaviour
    {
        // Members for orbit and fly cam
        public Vector3 targetOffset;
        public float distance = 5.0f;
        public float maxDistance = 500;
        public float minDistance = 0.5f;
        public float xSpeed = 200.0f;
        public float ySpeed = 200.0f;
        public int pitchMin = -22;
        public int pitchMax = 53;
        public int zoomRate = 1;
        public float panSpeed = 5.0f;
        public float zoomDampening = 5.0f;
        public float forceBasedPanSpeed = 100.0f;

        // Original members
        public float RotationDeltaRate = 20;
        public float TranslationDeltaRate = 50.0f;
        public float TouchTranslationDeltaRate = 0.2f;
        public bool InvertY = false;
        public GameObject altitudeIcon, moveIcon, orbitIcon;

        private float _camPitch = 60;
        private float _yaw;
        private Vector3 _lastMove;
        private Quaternion _cameraOrientation;
        private float momentumStartTime;

        private bool _moving;
        private bool _altitudeChanging;
        private bool _rotating;

        // Should be set by loader script as this is very much model specific
        Vector3 _minPosition;
        Vector3 _maxPosition;

        public void SetInputLimits(Vector3 min, Vector3 max)
        {
            Debug.Log("Min: " + min);
            Debug.Log("Max: " + max);
            _minPosition = min;
            _maxPosition = max;
        }

        private static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360)
                angle += 360;
            if (angle > 360)
                angle -= 360;
            return Mathf.Clamp(angle, min, max);
        }

        private void SetObjectActive(GameObject objectToSet, bool active)
        {
            if (objectToSet != null)
            {
                objectToSet.SetActive(active);
            }
        }

        private void SetMoveIconActive(bool active)
        {
            SetObjectActive(moveIcon, active);
        }

        private void SetAltitudeIconActive(bool active)
        {
            SetObjectActive(altitudeIcon, active);
        }

        private void SetOrbitIconActive(bool active)
        {
            SetObjectActive(orbitIcon, active);
        }

        void Start()
        {
            SetMoveIconActive(false);
            SetOrbitIconActive(false);
            SetAltitudeIconActive(false);

            enabled = false;

#if UNITY_ANDROID
            TouchTranslationDeltaRate *= 2.5f;
#endif

        }

        public void NotifyOnTransformChange(Vector3 cameraPosition)
        {
            transform.position = cameraPosition;
            enabled = true;
        }

        public void NotifyReadyForControl()
        {
            _camPitch = transform.eulerAngles.x;
            _yaw = transform.eulerAngles.y;
            enabled = true;
        }

        private Vector3 GetTranslation()
        {
            // Get absolute value of mouse or keyboard or touch
            // Mouse
            Vector3 keyboardTranslation = Vector3.zero;

            keyboardTranslation.x = Input.GetAxis("Horizontal");
            keyboardTranslation.y = Input.GetAxis("Vertical");
            keyboardTranslation.z = Input.GetAxis("Forward");
            bool keyboardTranslated = keyboardTranslation != Vector3.zero;

            keyboardTranslation *= Time.deltaTime * TranslationDeltaRate;

            Vector3 touchTranslation = Vector3.zero;
            if (Input.touchCount == 1)
            {
                touchTranslation.x = Input.GetTouch(0).deltaPosition.x;
                touchTranslation.z = Input.GetTouch(0).deltaPosition.y;

            }
            else if (Input.touchCount == 2)
            {
                _altitudeChanging = true;
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

                touchTranslation.y = deltaMagnitudeDiff * TouchTranslationDeltaRate;
            }

            bool touchTranslated = touchTranslation != Vector3.zero;
            touchTranslation *= -TouchTranslationDeltaRate * 1.5f;

            Vector3 mouseTranslation = Vector3.zero;
            float scrollWheelSpeed = Input.GetAxis("Mouse ScrollWheel");
            if (Input.GetMouseButton(1) && Input.touchCount == 0)
            {
                Vector3 right = new Vector3(transform.right.x, 0.0f, transform.right.z);
                Vector3 forward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
                float dX = -Input.GetAxis("Mouse X");
                float dY = -Input.GetAxis("Mouse Y");
                mouseTranslation.x = dX = Mathf.Clamp(dX, -1.0f, 1.0f);
                mouseTranslation.z = dY = Mathf.Clamp(dY, -1.0f, 1.0f);
                right = right * dX;
                forward = forward * dY;

                mouseTranslation *= Time.deltaTime * TranslationDeltaRate * panSpeed;
            }
            else if ((Input.GetMouseButton(2) || Mathf.Abs(scrollWheelSpeed) > 0.05f) && Input.touchCount == 0)
            {
                if (Mathf.Abs(scrollWheelSpeed) < 0.05f)
                {
                    var mouseY = Input.GetAxis("Mouse Y");
                    if (mouseY > 0.005f)
                    {
                        scrollWheelSpeed = 0.1f;
                    }
                    else if (mouseY < -0.005)
                    {
                        scrollWheelSpeed = -0.1f;
                    }
                }
                scrollWheelSpeed = Mathf.Clamp(scrollWheelSpeed, -0.1f, 0.1f);
                var deltaForward = scrollWheelSpeed * Time.deltaTime * zoomRate * 2.5f;
                deltaForward = Mathf.Clamp(deltaForward, -7.0f, 7.0f);
                mouseTranslation = transform.forward * deltaForward;
                mouseTranslation = Quaternion.Euler(new Vector3(0f, -transform.eulerAngles.y, 0f)) * mouseTranslation;
            }
            bool mouseTranslated = mouseTranslation != Vector3.zero;

            if (mouseTranslated || touchTranslated || keyboardTranslated)
            {
                _moving = true;
                momentumStartTime = 0;
                Vector3 translation = Vector3.zero;
                translation.x = GetLargestAbs(mouseTranslation.x,
                    keyboardTranslation.x,
                    touchTranslation.x);

                translation.y = GetLargestAbs(mouseTranslation.y,
                    keyboardTranslation.y,
                    touchTranslation.y);

                translation.z = GetLargestAbs(mouseTranslation.z,
                    keyboardTranslation.z,
                    touchTranslation.z);

                return translation;
            }
            else
            {
                if (momentumStartTime == 0)
                {
                    momentumStartTime = Time.time;
                }
                float fraction = (Time.time - momentumStartTime) / 0.7f;
                return Vector3.Lerp(_lastMove, Vector3.zero, fraction);
            }
        }

        private float GetLargestAbs(float f1, float f2, float f3)
        {
            float af1 = Mathf.Abs(f1);
            float af2 = Mathf.Abs(f2);
            float af3 = Mathf.Abs(f3);

            if (af1 > af2)
            {
                if (af1 > af3)
                {
                    return f1;
                }
                else
                {
                    return f3;
                }
            }
            else
            {
                if (af2 > af3)
                {
                    return f2;
                }
                else
                {
                    return f3;
                }
            }

        }

        public bool KeyboardMoving
        {
            get
            {
                return momentumStartTime == 0 && (Input.GetAxis("Forward") != 0.0f || Input.GetAxis("Horizontal") != 0.0f);
            }
        }

        private Quaternion GetRotation()
        {
            float keyboardYaw = Input.GetAxis("HorizontalTurn") * Time.deltaTime * RotationDeltaRate;
            float keyboardPitch = -Input.GetAxis("VerticalTurn") * Time.deltaTime * RotationDeltaRate * (InvertY ? -1f : 1f);

            _rotating = keyboardYaw != 0.0f || keyboardPitch != 0.0f;

            float touchYaw = 0.0f;
            float touchPitch = 0.0f;

            if (Input.touchCount == 3)
            {
                touchYaw = Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 0.4f;
                touchPitch = -Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 0.4f;
                _rotating = true;
            }

            float mouseYaw = 0.0f;
            float mousePitch = 0.0f;

            if (Input.touchCount == 0 && Input.GetMouseButton(0))
            {
                mouseYaw = Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                mousePitch = -Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
                _rotating = true;
            }

            _yaw += GetLargestAbs(keyboardYaw, touchYaw, mouseYaw);
            _camPitch += GetLargestAbs(keyboardPitch, touchPitch, mousePitch);
            _camPitch = ClampAngle(_camPitch, pitchMin, pitchMax);

            Quaternion rotation = Quaternion.identity;
            rotation.eulerAngles = new Vector3(LimitAngles(_camPitch), LimitAngles(_yaw), 0.0f);

            return rotation;
        }

        private Vector3 ClampPosition(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, _minPosition.x, _maxPosition.x);
            position.y = Mathf.Clamp(position.y, _minPosition.y, _maxPosition.y);
            position.z = Mathf.Clamp(position.z, _minPosition.z, _maxPosition.z);
            return position;
        }

        private void Update()
        {
            _moving = _rotating = _altitudeChanging = false;
            _lastMove = GetTranslation();
            _cameraOrientation = GetRotation();

            transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation, Time.time);

            var movementVector = _lastMove;
            var movementRotation = Quaternion.Euler(new Vector3(0f, transform.eulerAngles.y, 0f));
            movementVector = movementRotation * movementVector;
            transform.Translate(movementVector, Space.World);
            transform.position = ClampPosition(transform.position);

            if(_moving)
            {
                SetMoveIconActive(true);
                SetOrbitIconActive(false);
                SetAltitudeIconActive(false);
            } else if(_altitudeChanging)
            {
                SetMoveIconActive(false);
                SetOrbitIconActive(false);
                SetAltitudeIconActive(true);
            } else if(_rotating)
            {
                SetMoveIconActive(false);
                SetOrbitIconActive(true);
                SetAltitudeIconActive(false);
            } else
            {
                SetMoveIconActive(false);
                SetOrbitIconActive(false);
                SetAltitudeIconActive(false);
            }
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