namespace Pyrite
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Extensions;
    using Microsoft.Xna.Framework;
    using Model;
    using UnityEngine;

    public class PyriteOctreeLoader : MonoBehaviour
    {
        [Tooltip("Tracking Rig")]
        public GameObject CameraRig;

        [Header("Tracking Cubes")]
        public GameObject MeshPlaceholder;
        public GameObject OctreeTrackingCube;

        [Header("Server Options")]
        public string PyriteServer;

        [Header("Set Options (required)")]
        public int DetailLevel = 2;
        public bool FilterDetailLevels = false;
        public List<int> DetailLevelsToFilter;
        public string ModelVersion = "V1";
        public string SetName;
        public int MaxListCount = 50;

        [Header("Other Options")]
        public float UpgradeFactor = 1.05f;
        public float UpgradeConstant = 0.0f;
        public float DowngradeFactor = 1.05f;
        public float DowngradeConstant = 0.0f;

        private Vector3 tempPosition;
        private PyriteCube cubeCamPos;
        private PyriteCube cubeCamPosNew;
        private PyriteQuery pyriteQuery;
        private PyriteSetVersionDetailLevel pyriteLevel;

        protected bool Loaded { get; private set; }
        Dictionary<string, CubeTracker> cubeDict = new Dictionary<string, CubeTracker>();        
        LinkedList<CubeTracker> cubeList = new LinkedList<CubeTracker>();
        
        void Start()
        {
            if (string.IsNullOrEmpty(SetName))
            {
                Debug.LogError("Must specify SetName");
                return;
            }

            if (string.IsNullOrEmpty(ModelVersion))
            {
                Debug.LogError("Must specify ModelVersion");
                return;
            }

            StartCoroutine(InternalLoad());
        }

        void Update()
        {
            if (Loaded)
            {
                cubeCamPosNew = pyriteLevel.GetCubeForUnityWorldCoordinates(CameraRig.transform.position);
                if (!cubeCamPos.Equals(cubeCamPosNew))
                {
                    Debug.Log(String.Format("NEW CUBE POSITION: ({0},{1},{2})", cubeCamPosNew.X, cubeCamPosNew.Y, cubeCamPosNew.Z));
                    cubeCamPos = cubeCamPosNew;
                    LoadCamCubes();
                }

                var planePoint = CameraRig.transform.position;
                planePoint.y = 0f;
                Debug.DrawLine(CameraRig.transform.position, planePoint, Color.green, 0f, true);
            }
        }

        private IEnumerator InternalLoad()
        {
            yield return StartCoroutine(Load());
            Loaded = true;
        }

        IEnumerator Load()
        {            
            tempPosition = transform.position;
            transform.position = Vector3.zero;

            pyriteQuery = new PyriteQuery(this, 
                SetName, 
                ModelVersion, 
                PyriteServer, 
                UpgradeFactor, 
                UpgradeConstant,
                DowngradeFactor, 
                DowngradeConstant);
            yield return StartCoroutine(pyriteQuery.LoadAll(FilterDetailLevels ? DetailLevelsToFilter : null));
            var initialDetailLevelIndex = DetailLevel - 1;
            
            pyriteLevel = pyriteQuery.DetailLevels[DetailLevel];            
            var setSize = pyriteLevel.SetSize;
            Debug.Log("Set Size " + setSize);

            cubeCamPos = pyriteLevel.GetCubeForUnityWorldCoordinates(CameraRig.transform.position);
            LoadCamCubes();
            
            var worldObject = new GameObject("WorldParent") as GameObject;
            worldObject.transform.position = Vector3.zero;
            worldObject.transform.rotation = Quaternion.identity;
            foreach (var i in pyriteLevel.Octree.AllItems())
            {
                var pCube = CreateCubeFromCubeBounds(i);
                var cubePos = pyriteLevel.GetUnityWorldCoordinatesForCube(pCube);

                var loc = Instantiate(MeshPlaceholder, cubePos, Quaternion.identity) as GameObject;                
                loc.name = string.Format("Mesh:{0},{1},{2}", pCube.X, pCube.Y, pCube.Z);
                loc.transform.localScale = new Vector3(
                          pyriteLevel.WorldCubeScale.x,
                          pyriteLevel.WorldCubeScale.z,
                          pyriteLevel.WorldCubeScale.y);
                loc.transform.parent = worldObject.transform;
            }

         
            transform.position = tempPosition;
            Loaded = true;
        }

        private PyriteCube CreateCubeFromCubeBounds(CubeBounds cubeBounds)
        {
            return new PyriteCube
            {
                X = (int)cubeBounds.BoundingBox.Min.x,
                Y = (int)cubeBounds.BoundingBox.Min.y,
                Z = (int)cubeBounds.BoundingBox.Min.z
            };
        }


        void LoadCamCubes()
        {
            Debug.Log(String.Format("LoadCamCubes: ({0},{1},{2})", cubeCamPos.X, cubeCamPos.Y, cubeCamPos.Z));
            var cubeCamVector = new Vector3(cubeCamPos.X + 0.5f, cubeCamPos.Y + 0.5f, cubeCamPos.Z + 0.5f);
            
            var minVector = cubeCamVector - Vector3.one;
            var maxVector = cubeCamVector + Vector3.one;                        
            
            var octIntCubes = pyriteLevel.Octree.AllIntersections(new BoundingBox(minVector, maxVector));

            int cubeCounter = 0;
            foreach (var i in octIntCubes)
            {
                cubeCounter++;
                var pCube = CreateCubeFromCubeBounds(i.Object);
                var cubePos = pyriteLevel.GetUnityWorldCoordinatesForCube(pCube);

                // Setup object at cube location
                if (cubeDict.ContainsKey(pCube.GetKey()))
                {
                    var cube = cubeDict[pCube.GetKey()];
                    cubeList.Remove(cube);
                    cubeList.AddFirst(cube);
                    if (!cube.Active)
                    {
                        cube.Active = true;
                        // TODO: Re-activate cube
                    }
                }
                else
                {
                    CubeTracker ct;
                    // TODO: Create GameObject
                    
                    var gObj = Instantiate(OctreeTrackingCube, cubePos, Quaternion.identity) as GameObject;
                    gObj.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x * 0.8f,
                        pyriteLevel.WorldCubeScale.y * 0.8f, 
                        pyriteLevel.WorldCubeScale.z * 0.8f);

                    if (cubeList.Count < MaxListCount)
                    {
                        ct = new CubeTracker(pCube.GetKey(), null);
                    }
                    else
                    {
                        // Reuse Last CubeTracker
                        Debug.Log("Reusing Cube");
                        ct = cubeList.Last.Value;
                        cubeList.RemoveLast();
                        cubeDict.Remove(ct.DictKey);
                        ct.DictKey = pCube.GetKey();

                        // TODO: Reassign GameObject Content instead of destroying
                        Destroy(ct.gameObject);

                        if (ct.Active)
                        {
                            Debug.Log("ALERT: Active Object in List Tail");
                        }
                    }
                    gObj.transform.parent = gameObject.transform;
                    ct.gameObject = gObj;
                    ct.Active = true;
                    cubeList.AddFirst(ct);
                    cubeDict.Add(ct.DictKey, ct);
                }
            }
            Debug.Log(String.Format("CubeCounter: {0}  CubeList/Dict: {1}/{2}", cubeCounter, cubeList.Count, cubeDict.Count));

            foreach (var q in cubeList.Skip(cubeCounter).TakeWhile(q => q.Active))
            {
                q.Active = false;
            }
        }
    }
}