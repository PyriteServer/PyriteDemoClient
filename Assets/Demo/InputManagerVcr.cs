using UnityEngine;
using System.Collections;
using System.IO;
using System;

public class InputManagerVcr : MonoBehaviour
{
    public float RotationDeltaRate = 90;
    public float TranslationDeltaRate = 50.0f;
	public float TouchTranslationDeltaRate = 2.0f;

    private float _camPitch = 30;
    private float _yaw = 0;
    float _moveX;
    float _moveY;
    float _moveZ;
    private Quaternion _cameraOrientation;
    private Quaternion _rigOrientation;
    private Camera _pivotCamera;
    private Transform _targetPosition;

    // VCR
    public string VcrFilename;
    private bool useVCR;
    private InputVCR vcr;

    private bool epicInitiated = false;

    void Awake()
    {
        Transform root = transform;
        while (root.parent != null)
            root = root.parent;
        vcr = root.GetComponent<InputVCR>();
        useVCR = vcr != null;
        
        if (useVCR && vcr.mode == InputVCRMode.Record)
        {           
            vcr.NewRecording(); 
        }

        if (useVCR && vcr.mode == InputVCRMode.Playback)
        {
            Recording recording = Recording.ParseRecording(File.ReadAllText(VcrFilename));
            vcr.Play(recording);
        }
            
    }

    void OnDestroy()
    {
        if (useVCR && vcr.mode == InputVCRMode.Record)
        {
            File.WriteAllText(VcrFilename, vcr.GetRecording().ToString());            
        }
    }

    void Start()
    {
        _pivotCamera = GetComponentInChildren<Camera>();
        _targetPosition = transform;        
    }    

    void FixedUpdate()
    {
        if (!epicInitiated && Input.GetButton("Fire1") || Input.GetButton("XboxA"))
        {
            epicInitiated = true;
            EpicFail();
        }

        if (!epicInitiated && Input.GetButton("Fire2") || Input.GetButton("XboxB"))
        {
            epicInitiated = false;
            EpicUnFail();            
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {

            // Get movement of the finger since last frame
            Vector2 touchDeltaPosition = Input.GetTouch(0).deltaPosition;

            // Move object across XY plane
            _targetPosition.Translate(
                -touchDeltaPosition.x * TouchTranslationDeltaRate,
                -touchDeltaPosition.y * TouchTranslationDeltaRate,
                0);
        }
        else
        {
            _moveX = vcr.GetAxis("Horizontal") * Time.deltaTime * TranslationDeltaRate;
            _moveY = vcr.GetAxis("Vertical") * Time.deltaTime * TranslationDeltaRate;
            _moveZ = vcr.GetAxis("Forward") * Time.deltaTime * TranslationDeltaRate;

            _yaw += vcr.GetAxis("HorizontalTurn") * Time.deltaTime * RotationDeltaRate;
            _camPitch += vcr.GetAxis("VerticalTurn") * Time.deltaTime * RotationDeltaRate;

            if (vcr.GetButton("XboxLB"))
            {
                _moveY -= Time.deltaTime * TranslationDeltaRate;
            }
            if (vcr.GetButton("XboxRB"))
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

    private void EpicFail()
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag("MeshCube");
        foreach (var go in objects)
        {
            var collider = go.AddComponent<BoxCollider>();
            var rigid = go.AddComponent<Rigidbody>();
            if (rigid != null)
                rigid.mass = 1000;
        }
    }

    private void EpicUnFail()
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag("MeshCube");
        foreach (var go in objects)
        {
            Destroy(go.GetComponent<BoxCollider>());
            Destroy(go.GetComponent<Rigidbody>());
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
        }
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
