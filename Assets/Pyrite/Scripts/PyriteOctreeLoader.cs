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

        [Header("Pyrite Options")]
        public string PyriteServer;
        public string ModelVersion = "V1";
        public string SetName;
        public int DetailLevel = 2;
        public bool FilterDetailLevels = false;
        public List<int> DetailLevelsToFilter;
        public float UpgradeFactor = 1.05f;
        public float UpgradeConstant = 0.0f;
        public float DowngradeFactor = 1.05f;
        public float DowngradeConstant = 0.0f;

        [Header("Pyrite Options")]
        public float WorldYOffset = 450;

        [Header("Octree Options")]        
        public bool ShowDebugCubes = false;
        public bool ShowLocatorCubes = false;
        public bool ShowCubes = false;
        public int OctreeListCount = 50;
        public GameObject RenderCube;
        public GameObject TranslucentCube;
        public GameObject LocatorCube;
        
        private Vector3 tempPosition;
        private PyriteCube cubeCamPos;
        private PyriteCube cubeCamPosNew;
        private PyriteQuery pyriteQuery;
        private PyriteSetVersionDetailLevel pyriteLevel;
        private GameObject OctreeTracking;

        // Octree Internal Tracking
        protected bool Loaded { get; private set; }
        private Dictionary<string, CubeTracker> cubeDict = new Dictionary<string, CubeTracker>();        
        private LinkedList<CubeTracker> cubeList = new LinkedList<CubeTracker>();

        // Processing Queues

        
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
                var adjustedPos = new Vector3(
                    -CameraRig.transform.position.x,
                    -CameraRig.transform.position.z,
                    CameraRig.transform.position.y - WorldYOffset);
                cubeCamPosNew = pyriteLevel.GetCubeForWorldCoordinates(adjustedPos);
                if (!cubeCamPos.Equals(cubeCamPosNew))
                {
                    Debug.Log(String.Format("NEW CUBE POSITION: ({0},{1},{2})", cubeCamPosNew.X, cubeCamPosNew.Y, cubeCamPosNew.Z));
                    cubeCamPos = cubeCamPosNew;
                    LoadCamCubes();
                }

                // Debugging Option
                if (ShowDebugCubes)
                {
                    var planePoint = CameraRig.transform.position;
                    planePoint.y = 0f;
                    Debug.DrawLine(CameraRig.transform.position, planePoint, Color.green, 0f, true);
                }
            }
        }

        private IEnumerator InternalLoad()
        {
            yield return StartCoroutine(Load());
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
            
            pyriteLevel = pyriteQuery.DetailLevels[DetailLevel];            
            var setSize = pyriteLevel.SetSize;
            Debug.Log("Set Size " + setSize);

            var adjustedPos = new Vector3(
                -CameraRig.transform.position.x,
                -CameraRig.transform.position.z,
                CameraRig.transform.position.y - WorldYOffset);
            cubeCamPos = pyriteLevel.GetCubeForWorldCoordinates(adjustedPos);
                        
            var worldObject = new GameObject("OctreeParent") as GameObject;
            worldObject.transform.position = Vector3.zero;
            worldObject.transform.rotation = Quaternion.identity;
            OctreeTracking = new GameObject("OctreeTracking") as GameObject;
            OctreeTracking.transform.position = Vector3.zero;
            OctreeTracking.transform.rotation = Quaternion.identity;
            
            if (ShowCubes)
            {
                foreach (var i in pyriteLevel.Octree.AllItems())
                {
                    var pCube = CreateCubeFromCubeBounds(i);
                    var cubePos = pyriteLevel.GetWorldCoordinatesForCube(pCube);

                    var adjustedCubePos = new Vector3(
                        -cubePos.x,
                        cubePos.z + WorldYOffset,
                        -cubePos.y);
                    var loc = Instantiate(TranslucentCube, adjustedCubePos, Quaternion.identity) as GameObject;
                    loc.name = string.Format("Mesh:{0},{1},{2}", pCube.X, pCube.Y, pCube.Z);
                    loc.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);
                    loc.transform.parent = worldObject.transform;
                }
            }

            transform.position = tempPosition;            
            LoadCamCubes();
        }

        void LoadCamCubes()
        {
            //for (int detailLevel = pyriteQuery.DetailLevels.Length - 1; detailLevel >= 0; detailLevel--)            
            {
                var detailLevel = DetailLevel;

                var pLevel = pyriteQuery.DetailLevels[detailLevel];                
                var cPos = pLevel.GetCubeForWorldCoordinates(new Vector3(
                    -CameraRig.transform.position.x,
                    -CameraRig.transform.position.z,
                    CameraRig.transform.position.y - WorldYOffset));

                Debug.Log(String.Format("LoadCamCubes: ({0},{1},{2})", cPos.X, cPos.Y, cPos.Z));
                var cubeCamVector = new Vector3(cPos.X + 0.5f, cPos.Y + 0.5f, cPos.Z + 0.5f);
                var minVector = cubeCamVector - Vector3.one;
                var maxVector = cubeCamVector + Vector3.one;
                int cubeCounter = 0;
                var octIntCubes = pLevel.Octree.AllIntersections(new BoundingBox(minVector, maxVector));
                foreach (var i in octIntCubes)
                {
                    cubeCounter++;
                    var pCube = CreateCubeFromCubeBounds(i.Object);
                    var cubeKey = string.Format("{0},{1}", detailLevel, pCube.GetKey());
                    var cubePos = pLevel.GetWorldCoordinatesForCube(pCube);

                    CubeTracker ct = null;                    

                    // Setup object at cube location
                    if (cubeDict.ContainsKey(cubeKey))
                    {
                        ct = cubeDict[cubeKey];
                        cubeList.Remove(ct);
                        cubeList.AddFirst(ct);
                        if (!ct.Active)
                        {
                            ct.Active = true;   // TODO: Active status should retain mesh, re-display NO NEED TO RELOAD                            

                            if (ShowLocatorCubes)
                            {
                                ct.trackObject.GetComponent<MeshRenderer>().material.color = Color.green;
                                ct.trackObject.SetActive(true);
                            }
                        }
                    }
                    else
                    {
                        if (cubeList.Count < OctreeListCount)
                        {
                            ct = new CubeTracker(cubeKey);
                        }
                        else
                        {
                            // Reuse Last CubeTracker
                            Debug.Log("Reusing Cube");
                            ct = cubeList.Last.Value;
                            if (ct.Active)
                            {
                                Debug.Log(">>>>>>>>> ERROR: Active Object in List Tail. Too many required cubes.");
                                return;
                            }
                            cubeList.RemoveLast();
                            cubeDict.Remove(ct.DictKey);
                            ct.DictKey = cubeKey;

                            if (ct.gameObject != null)
                            {
                                ct.ClearMesh();
                                Destroy(ct.gameObject);
                            }
                        }
                        // TODO: Load Mesh for Cube Position
                        //var loadRequest = new LoadCubeRequest(
                        //    pCube.X,
                        //    pCube.Y,
                        //    pCube.Z,
                        //    detailLevel, pyriteQuery, createdObject =>
                        //    {
                        //        ct.gameObject = createdObject;
                        //    });
                        //StartCoroutine(EnqueueLoadCubeRequest(loadRequest));

                        // Setup Locator GameObject
                        var adjustedPos = new Vector3(
                            -cubePos.x,
                            cubePos.z + WorldYOffset,
                            -cubePos.y);
                        GameObject gObj = null;
                        if (ShowLocatorCubes)
                        {
                            if (ct.trackObject)
                            {
                                ct.trackObject.transform.position = adjustedPos;
                                ct.trackObject.transform.localScale = new Vector3(
                                    pLevel.WorldCubeScale.x * .8f,
                                    pLevel.WorldCubeScale.y * .8f,
                                    pLevel.WorldCubeScale.z * .8f);
                                ct.trackObject.SetActive(true);                                
                                ct.trackObject.GetComponent<MeshRenderer>().material.color = Color.yellow;
                            }
                            else
                            {
                                gObj = Instantiate(LocatorCube, adjustedPos, Quaternion.identity) as GameObject;
                                gObj.transform.localScale = new Vector3(
                                    pLevel.WorldCubeScale.x * .8f,
                                    pLevel.WorldCubeScale.y * .8f,
                                    pLevel.WorldCubeScale.z * .8f);
                                gObj.transform.parent = OctreeTracking.transform;
                                ct.trackObject = gObj;
                            }
                        }

                        ct.pyriteQuery = pyriteQuery;
                        ct.Active = true;
                        cubeList.AddFirst(ct);
                        cubeDict.Add(ct.DictKey, ct);
                    }
                }
                Debug.Log(String.Format("CubeCounter: {0}  CubeList/Dict: {1}/{2}", cubeCounter, cubeList.Count,
                    cubeDict.Count));

                int levelCount = 0;
                foreach (var cube in cubeList)
                {
                    if (cube.l == detailLevel)
                    {
                        levelCount++;
                        if (levelCount > cubeCounter)
                        {
                            if (!cube.Active)
                            {
                                //Debug.Log("Break List OPTION");
                                //break;
                            }
                            // TODO: Create Interim status Cube without auto cleanup
                            cube.Active = false;
                            //cube.ClearMesh();     // TODO: Should NOT DELETE MESH

                            if (ShowLocatorCubes && cube.trackObject)
                            {
                                if (ShowLocatorCubes)
                                    cube.trackObject.GetComponent<MeshRenderer>().material.color = Color.red;
                                else
                                    cube.trackObject.SetActive(false);
                            }
                        }
                    }
                }
            }
        }
    }
}