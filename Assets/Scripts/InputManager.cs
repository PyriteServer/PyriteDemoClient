using UnityEngine;

namespace PyriteDemoClient
{
    using UnityStandardAssets.CrossPlatformInput;

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
        public int yMinLimit = -80;
        public int yMaxLimit = 80;
        public int zoomRate = 1;
        public float panSpeed = 5.0f;
        public float zoomDampening = 5.0f;
        public float forceBasedPanSpeed = 100.0f;
        private Vector3 position;
        float speed = 0.0f;
        public bool forceBasedPanningAndFovZoom = false;

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

        public void EnableCameraFly()
        {
            //cameraFlyEnabled = true;

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
            SetMoveIconActive(false);

            float scrollWheelSpeed = Input.GetAxis("Mouse ScrollWheel");

            if (forceBasedPanningAndFovZoom)
            {
                Debug.Log(scrollWheelSpeed);
                Camera.main.fieldOfView -= scrollWheelSpeed * Time.deltaTime * zoomRate;
            }
            
            if (scrollWheelSpeed > 0.0f || scrollWheelSpeed < 0.0f)
            {
                if (scrollWheelSpeed > 0.005f)
                {
                    scrollWheelSpeed = 0.1f;
                }
                else if (scrollWheelSpeed < -0.005)
                {
                    scrollWheelSpeed = -0.1f;
                } 
                SetMoveIconActive(true);
            }

            if (Input.GetMouseButton(2) || Mathf.Abs(scrollWheelSpeed) > 0.005)
            {
                if (forceBasedPanningAndFovZoom)
                {
                    Camera.main.fieldOfView -= Input.GetAxis("Mouse Y") * Time.deltaTime * zoomRate * 0.125f;
                }
                else if (Mathf.Abs(scrollWheelSpeed) < 0.005)
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
                var deltaForward = scrollWheelSpeed * Time.deltaTime * zoomRate * 10;

                transform.Translate(transform.forward * deltaForward, Space.World);
                SetMoveIconActive(true);
            }
            else if (Input.GetMouseButton(0))
            {
                _yaw += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                _camPitch -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                _camPitch = ClampAngle(_camPitch, yMinLimit, yMaxLimit);


                SetOrbitIconActive(true);
            }
            else if (Input.GetMouseButton(1)) // wasd
            {
                //target.rotation = transform.rotation;

                Vector3 right = new Vector3(transform.right.x, 0.0f, transform.right.z);
                Vector3 forward = new Vector3(transform.forward.x, 0.0f, transform.forward.z);

                right = right * -Input.GetAxis("Mouse X");
                forward = forward * -Input.GetAxis("Mouse Y");

                Rigidbody rb = GetComponent<Rigidbody>();
                    
                if (forceBasedPanningAndFovZoom)
                {
                    rb.constraints = RigidbodyConstraints.FreezePositionY;
                    Vector3 force = right + forward;
                    rb.AddForce(force * forceBasedPanSpeed);

                }
                else
                {
                    transform.Translate(right * panSpeed, Space.World);
                    transform.Translate(forward * panSpeed, Space.World);
                }
                
                SetMoveIconActive(true);
            }   else
            {
                processedInput = false;
            }
            
            if ( !forceBasedPanningAndFovZoom && (lastProcessed == InputProcessor.OrbitCamera || processedInput))
            {
                _cameraOrientation.eulerAngles = new Vector3(_camPitch, _yaw, 0);
                transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation, Time.deltaTime * zoomDampening);
            }

            return processedInput;
        }

        private void ProcessFlyCameraInput()
        {
            if (speed > 0.0f)
            {
                speed -= Time.deltaTime * (zoomRate / 2.0f);
                if (speed < 0.0f)
                {
                    speed = 0.0f;
                    SetMoveIconActive(false);
                }
            }

            Vector3 xAxis = Vector3.zero;
            Vector3 yAxis = Vector3.zero;

            if (Input.GetMouseButton(1))
            {
                xAxis = (transform.right * -Input.GetAxis("Mouse X") * panSpeed);
                yAxis = (transform.up * -Input.GetAxis("Mouse Y") * panSpeed);
                speed = 0.0f;
                SetMoveIconActive(true);
            }
            else
            {
                _yaw += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                _camPitch -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                _camPitch = ClampAngle(_camPitch, yMinLimit, yMaxLimit);

                var desiredRotation = Quaternion.Euler(_camPitch, _yaw, 0);

                transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime * zoomDampening);
            }


            if (Input.GetMouseButton(0))
            {
                speed += Time.deltaTime * zoomRate;

                if (speed > 0.0f)
                {
                    SetMoveIconActive(true);
                }
            }

            position = position + (transform.forward * speed) + xAxis + yAxis;

            transform.position = position;
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

            if(dX != 0.0f || dY != 0.0f || dZ != 0.0f || dYaw != 0.0f || dPitch != 0.0f)
            {
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
            _camPitch = Mathf.Clamp(_camPitch, -60, 80);
            
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

            position = transform.position;
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