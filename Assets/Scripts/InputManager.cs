using UnityEngine;
	
namespace PyriteDemoClient
{
    using UnityStandardAssets.CrossPlatformInput;
    
    public class InputManager : MonoBehaviour
    {
        // Members for orbit and fly cam
        public Transform target;
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
        private float xDeg = 0.0f;
        private float yDeg = 0.0f;
        private float currentDistance;
        private float desiredDistance;
        private Quaternion currentRotation;
        private Quaternion desiredRotation;
        private Quaternion rotation;
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
        private float _moveX;
        private float _moveY;
        private float _moveZ;
		private Vector3 _lastMove;
		private Vector3 _thisMove;
        private Quaternion _cameraOrientation;
        private Camera _pivotCamera;
        private Transform _targetPosition;
		private float momentumStartTime;

        bool cameraFlyEnabled = false;

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
            
            // Need to create camera target for panning here as PyriteLoader is changing CameraRig transformation on-the-go.
            if (!target)
            {
                Mesh mesh = Resources.Load("Cube") as Mesh;
                GameObject go = new GameObject("Cam Target");
                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
                
                go.transform.position = transform.position + (transform.forward * distance);
                target = go.transform;
            }
            else
            {
                target.transform.position = transform.position + (transform.forward * distance);
            }

            distance = Vector3.Distance(transform.position, target.position);
            currentDistance = distance;
            desiredDistance = distance;

            position = transform.position;
            rotation = transform.rotation;
            currentRotation = transform.rotation;
            desiredRotation = transform.rotation;

            xDeg = Vector3.Angle(Vector3.right, transform.right);
            yDeg = Vector3.Angle(Vector3.up, transform.up);

            
            enabled = true;
        }

        private void ProcessOrbitCameraInput()
        {

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
                SetMoveIconActive(true);
            }
            
            if (Input.GetMouseButton(2))
            {
                if (forceBasedPanningAndFovZoom)
                {
                    Camera.main.fieldOfView -= Input.GetAxis("Mouse Y") * Time.deltaTime * zoomRate * 0.125f;
                }
                else
                {
                    desiredDistance -= Input.GetAxis("Mouse Y") * Time.deltaTime * zoomRate * 0.125f * Mathf.Abs(desiredDistance);
                }
                
                SetMoveIconActive(true);
            }
            else if(Input.GetMouseButton(0))
            {
                xDeg += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                yDeg -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                yDeg = ClampAngle(yDeg, yMinLimit, yMaxLimit);
                
                desiredRotation = Quaternion.Euler(yDeg, xDeg, 0);
                currentRotation = transform.rotation;

                rotation = Quaternion.Lerp(currentRotation, desiredRotation, Time.deltaTime * zoomDampening);
                transform.rotation = rotation;

                SetOrbitIconActive(true);
            }
            else if (Input.GetMouseButton(1))
            {
                target.rotation = transform.rotation;

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
                    target.Translate(right * panSpeed, Space.World);
                    target.Translate(forward * panSpeed, Space.World);
                }
                
                SetMoveIconActive(true);
            }
            
            
            if ( !forceBasedPanningAndFovZoom)
            {
                desiredDistance -= scrollWheelSpeed * Time.deltaTime * zoomRate * Mathf.Abs(desiredDistance);
                desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
                currentDistance = Mathf.Lerp(currentDistance, desiredDistance, Time.deltaTime * zoomDampening);
                position = target.position - (transform.forward * currentDistance + targetOffset);
                transform.position = position;    
            }
            
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
                xDeg += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                yDeg -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;

                yDeg = ClampAngle(yDeg, yMinLimit, yMaxLimit);

                desiredRotation = Quaternion.Euler(yDeg, xDeg, 0);
                currentRotation = transform.rotation;

                rotation = Quaternion.Lerp(currentRotation, desiredRotation, Time.deltaTime * zoomDampening);
                transform.rotation = rotation;
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

        void ProcessOriginalInput()
        {
            
            // Keyboard
            _moveX = Input.GetAxis("Horizontal") * Time.deltaTime * TranslationDeltaRate;
            _moveY = Input.GetAxis("Vertical") * Time.deltaTime * TranslationDeltaRate;
            _moveZ = Input.GetAxis("Forward") * Time.deltaTime * TranslationDeltaRate;

            _yaw += Input.GetAxis("HorizontalTurn") * Time.deltaTime * RotationDeltaRate;
            _camPitch -= Input.GetAxis("VerticalTurn") * Time.deltaTime * RotationDeltaRate * (InvertY ? -1f : 1f);

            // Touch
            if (Input.touchCount == 1)
            {
                _moveX = Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 1.5f;
                _moveZ = Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 1.5f;

                if (orbitIcon != null)
                {
                    SetMoveIconActive(true);
                    SetOrbitIconActive(false);
                    SetAltitudeIconActive(false);
                }
            }

            if (Input.touchCount == 3)
            {
                _yaw += Input.GetTouch(0).deltaPosition.x * -TouchTranslationDeltaRate * 0.4f;
                _camPitch -= Input.GetTouch(0).deltaPosition.y * -TouchTranslationDeltaRate * 0.4f;

                if (orbitIcon != null)
                {
                    SetOrbitIconActive(true);
                    SetMoveIconActive(false);
                    SetAltitudeIconActive(false);
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
                _moveY -= Time.deltaTime * TranslationDeltaRate;
            }
            if (Input.GetButton("XboxRB"))
            {
                _moveY += Time.deltaTime * TranslationDeltaRate;
            }

            // Handle momentum
            if ((_moveX == 0 && _moveY == 0 && _moveZ == 0))
            {
                if (momentumStartTime == 0)
                {
                    momentumStartTime = Time.time;
                }
                float fraction = (Time.time - momentumStartTime) / 0.7f;
                var momentum = Vector3.Lerp(_lastMove, Vector3.zero, fraction);
                _thisMove = new Vector3(
                    _moveX == 0 ? momentum.x : _moveX,
                    _moveY == 0 ? momentum.y : _moveY,
                    _moveZ == 0 ? momentum.z : _moveZ);

            }
            else
            {
                _lastMove = new Vector3(_moveX, _moveY, _moveZ);
                _thisMove = _lastMove;
                momentumStartTime = 0;
            }

            // Rotate
            _cameraOrientation.eulerAngles = new Vector3(LimitAngles(_camPitch), LimitAngles(_yaw), 0);
            transform.rotation = Quaternion.Lerp(transform.rotation, _cameraOrientation,
                                                 Time.time);

            // Translate
            var movementVector = _thisMove;
            var movementRotation = Quaternion.Euler(new Vector3(0f, transform.eulerAngles.y, 0f));
            movementVector = movementRotation * movementVector;
            transform.Translate(movementVector, Space.World);

            // var planePoint = transform.position;
            // planePoint.y = 0;
            // Debug.DrawLine(transform.position, planePoint, Color.green, 0f, true);
        }

        private void Update()
        {
            if ( cameraFlyEnabled )
            {
                
            }
            else
            {

                

                //ProcessFlyCameraInput();
            
                ProcessOrbitCameraInput();

                //ProcessOriginalInput();
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