namespace PyriteDemoClient
{
    using UnityEngine;

    enum InputProcessor
    {
        Original = 0,
        OrbitCamera
    }

    public class InputManager : MonoBehaviour
    {
        private InputProcessor lastProcessed = InputProcessor.Original;

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
        private Vector3 position;
        float speed = 0.0f;

        // Original members
        public float RotationDeltaRate = 20;
        public float TranslationDeltaRate = 50.0f;
        public float TouchTranslationDeltaRate = 0.2f;
        public bool InvertY = false;
        public GameObject altitudeIcon, moveIcon, orbitIcon;

        private float _camPitch = 30;
        private float _yaw;
        private Vector3 _lastMove;
        private Quaternion _cameraOrientation;
        private float momentumStartTime;

        // Should be set by loader script as this is very much model specific
        Vector3 _minPosition;
        Vector3 _maxPosition;

        public void EnableCameraFly()
        {
            //cameraFlyEnabled = true;

        }
        
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
            //Cursor.visible = false;

            SetMoveIconActive(false);
            SetOrbitIconActive(false);
            SetAltitudeIconActive(false);

            enabled = false;
        }
        
        public void NotifyOnTransformChange()
        {
            // Hardcoded initial position for camera
            // TODO: place dummy object representing init camera position into scene and grab the position from there
            transform.position = new Vector3(-248.2652f, 132, -250.901f);
            position = transform.position;
            enabled = true;
        }

        private bool ProcessOrbitCameraInput()
        {
            bool processedInput = true;
            SetOrbitIconActive(false);
            if (_lastMove != Vector3.zero)
            {
                SetMoveIconActive(false);
            }


            float scrollWheelSpeed = Input.GetAxis("Mouse ScrollWheel");


            Mathf.Clamp(scrollWheelSpeed, 0.1f, -0.1f);

            if (Input.GetMouseButton(2) || Mathf.Abs(scrollWheelSpeed) > 0.005)
            {
                if (Mathf.Abs(scrollWheelSpeed) < 0.005)
                {

                    var mouseY = Input.GetAxis("Mouse Y");
                    if (mouseY > 0.005f)
                    {
                        scrollWheelSpeed = 0.1f;
                    } else if (mouseY < -0.005)
                    {
                        scrollWheelSpeed = -0.1f;
                    }
                }
                var deltaForward = scrollWheelSpeed * Time.deltaTime * zoomRate * 5;
                _lastMove = transform.forward * deltaForward;
                //Debug.LogFormat("translation: {0}, {1}, {2}", translation.x, translation.y, translation.z);
                transform.Translate(_lastMove, Space.World);
                momentumStartTime = 0;
                SetMoveIconActive(true);
            }
            else if (Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                _camPitch -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                _camPitch = ClampAngle(_camPitch, pitchMin, pitchMax);
                
                SetOrbitIconActive(true);
            }
            else if (Input.GetMouseButton(1)) // wasd
            {
                Vector3 right = new Vector3(transform.right.x, 0.0f, transform.right.z);
                Vector3 forward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);
                float dX = -Input.GetAxis("Mouse X");
                float dY = -Input.GetAxis("Mouse Y");
                dX = Mathf.Clamp(dX, -1.0f, 1.0f);
                dY = Mathf.Clamp(dY, -1.0f, 1.0f);
                right = right * dX;
                forward = forward * dY; 
                _lastMove = (right + forward) * panSpeed;
                momentumStartTime = 0;
                transform.Translate(_lastMove, Space.World);
                SetMoveIconActive(true);
            }   else
            {
                processedInput = false;
            }
            
            if ( (lastProcessed == InputProcessor.OrbitCamera || processedInput))
            {
                if (!processedInput)
                {
                    if (momentumStartTime == 0)
                    {
                        momentumStartTime = Time.time;
                    }
                    float fraction = (Time.time - momentumStartTime)/0.7f;
                    var momentum = Vector3.Lerp(_lastMove, Vector3.zero, fraction);
                    var thisMove = new Vector3(
                        momentum.x,
                        momentum.y,
                        momentum.z);
                    transform.Translate(thisMove, Space.World);

                    if (thisMove != Vector3.zero)
                    {
                        SetMoveIconActive(true);
                    }
                }

                _cameraOrientation.eulerAngles = new Vector3(_camPitch, _yaw, 0);
                transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation, Time.deltaTime * zoomDampening);
            }

            return processedInput;
        }

        bool ProcessOriginalInput()
        {
            bool inputProcessed = false;
            // Keyboard
            var dX = Input.GetAxis("Horizontal") * Time.deltaTime * TranslationDeltaRate;
            var dY = Input.GetAxis("Vertical") * Time.deltaTime * TranslationDeltaRate;
            var dZ = Input.GetAxis("Forward") * Time.deltaTime * TranslationDeltaRate;
            float dYaw = Input.GetAxis("HorizontalTurn");
            float dPitch = Input.GetAxis("VerticalTurn");
            _yaw += Input.GetAxis("HorizontalTurn") * Time.deltaTime * RotationDeltaRate;
            _camPitch -= Input.GetAxis("VerticalTurn") * Time.deltaTime * RotationDeltaRate * (InvertY ? -1f : 1f);
            _camPitch = ClampAngle(_camPitch, pitchMin, pitchMax);

            if (dX != 0.0f || dY != 0.0f || dZ != 0.0f || dYaw != 0.0f || dPitch != 0.0f)
            {
                //Debug.LogFormat("dxyz: {0}, {1}, {2}", dX, dY, dZ);
                inputProcessed = true;
            }

            // Touch
            if (Input.touchCount == 1)
            {
                inputProcessed = true;
                dX = Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 1.5f;
                dZ = Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 1.5f;

                if (orbitIcon != null)
                {
                    SetMoveIconActive(true);
                    SetOrbitIconActive(false);
                    SetAltitudeIconActive(false);
                }
            } else if (Input.touchCount == 3)
            {
                inputProcessed = true;
                _yaw += Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 0.4f;
                _camPitch -= Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 0.4f;
                _camPitch = ClampAngle(_camPitch, pitchMin, pitchMax);

                if (orbitIcon != null)
                {
                    SetOrbitIconActive(true);
                    SetMoveIconActive(false);
                    SetAltitudeIconActive(false);
                }
            } else if (Input.touchCount == 2)
            {
                inputProcessed = true;
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

                dY = deltaMagnitudeDiff * TouchTranslationDeltaRate;

                if (orbitIcon != null)
                {
                    SetAltitudeIconActive(true);
                    SetOrbitIconActive(false);
                    SetMoveIconActive(false);
                }
            }

            if (Input.touchCount == 0 && orbitIcon != null)
            {

                SetOrbitIconActive(false);
                SetMoveIconActive(false);
                SetAltitudeIconActive(false);
            }

            // Limit movement
            _camPitch = ClampAngle(_camPitch, pitchMin, pitchMax);

            if (Input.GetButton("XboxLB"))
            {
                dY -= Time.deltaTime * TranslationDeltaRate;
            }
            if (Input.GetButton("XboxRB"))
            {
                dY += Time.deltaTime * TranslationDeltaRate;
            }
            if (inputProcessed || lastProcessed == InputProcessor.Original)
            {
                Vector3 thisMove;
                // Handle momentum
                if ((dX == 0 && dY == 0 && dZ == 0))
                {
                    if (momentumStartTime == 0)
                    {
                        momentumStartTime = Time.time;
                    }
                    float fraction = (Time.time - momentumStartTime) / 0.7f;
                    var momentum = Vector3.Lerp(_lastMove, Vector3.zero, fraction);
                    thisMove = new Vector3(
                        dX == 0 ? momentum.x : dX,
                        dY == 0 ? momentum.y : dY,
                        dZ == 0 ? momentum.z : dZ);

                }
                else
                {
                    _lastMove = new Vector3(dX, dY, dZ);
                    thisMove = _lastMove;
                    momentumStartTime = 0;
                }

                // Rotate
                _cameraOrientation.eulerAngles = new Vector3(LimitAngles(_camPitch), LimitAngles(_yaw), 0);
                transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation,
                                                        Time.time);

                // Translate
                var movementVector = thisMove;
                var movementRotation = Quaternion.Euler(new Vector3(0f, transform.eulerAngles.y, 0f));
                movementVector = movementRotation * movementVector;
                transform.Translate(movementVector, Space.World);
            }
            return inputProcessed;
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
            if (!ProcessOriginalInput()) // Keyboard + Touch
            {
                if(ProcessOrbitCameraInput()) // Mouse
                {
                    lastProcessed = InputProcessor.OrbitCamera;
                }
            } else
            {
                lastProcessed = InputProcessor.Original;
            }

            position = transform.position = ClampPosition(transform.position);
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