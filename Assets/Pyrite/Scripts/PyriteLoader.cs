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

    public class PyriteLoader : MonoBehaviour
    {
        private DictionaryCache<string, GeometryBuffer> _eboCache;

        private DictionaryCache<string, Material> _materialCache;

        private DictionaryCache<string, MaterialData> _materialDataCache;

        private readonly Dictionary<string, MaterialData> _partiallyConstructedMaterialDatas =
            new Dictionary<string, MaterialData>();

        // Debug Text Counters
        private readonly GUIStyle _guiStyle = new GUIStyle();
        private int EboCacheHits;
        private int EboCacheMisses;
        private int MaterialCacheHits;
        private int MaterialCacheMisses;

        private int FileCacheHits;
        private int FileCacheMisses;

        // Requests that were cancelled between queues
        private int CancelledRequests;
        // Requests that were cancelled by the time the cache tried to load it
        private int LateCancelledRequests;
        // End counter bits

        private const int RETRY_LIMIT = 2;

        private float _geometryBufferAltitudeTransform;

        [Tooltip("If set object will be moved closer to model being loaded")]
        public GameObject CameraRig;

        public bool EnableDebugLogs = false;

        [Tooltip("Prefab for detection cubes")]
        public GameObject PlaceHolderCube;

        [Tooltip("Prefab for base cube object that we will populate data")]
        public GameObject BaseModelCube;

        [Header("Server Options")]
        public string PyriteServer;

        [Range(0, 100)]
        public int MaxConcurrentRequests = 8;

        [Header("Set Options (required)")]
        public int DetailLevel = 6;

        public bool FilterDetailLevels = false;
        public List<int> DetailLevelsToFilter;
        public string ModelVersion = "V2";
        public string SetName;

        [Header("Performance options")]
        public int EboCacheSize = 250;

        public int MaterialCacheSize = 100;
        public int MaterialDataCacheSize = 100;

        [Header("Debug Options")]
        public bool UseCameraDetection = true;        
        public bool UseUnlitShader = true;
        public bool UseFileCache = true;
        public bool ShowDebugText = true;

        [Header("Octree Options")]
        public bool UseOctreeSelection = false;
        public int MaxListCount = 50;
        public GameObject OctreeTranslucenttCube;
        public GameObject OctreeMarkerCube;
        

        [Header("Other Options")]
        public float UpgradeFactor = 1.05f;

        public float UpgradeConstant = 0.0f;
        public float DowngradeFactor = 1.05f;
        public float DowngradeConstant = 0.0f;
        public bool UseWwwForTextures = false;
        public bool UseWwwForEbo = false;
        public bool CacheFill = false;
        public int CacheSize = 3000;

        [HideInInspector]
        public Plane[] CameraFrustrum;

        // Queue for requests that are waiting for their material data
        private readonly Queue<LoadCubeRequest> _loadMaterialQueue = new Queue<LoadCubeRequest>(10);

        // Queue textures that have been downloaded and now need to be constructed into material data
        private readonly Queue<KeyValuePair<string, byte[]>> _texturesReadyForMaterialDataConstruction =
            new Queue<KeyValuePair<string, byte[]>>(5);

        // Dictionary list to keep track of requests that are dependent on some other in progress item (e.g. material data or model data loading)
        private readonly Dictionary<string, LinkedList<LoadCubeRequest>> _dependentCubes =
            new Dictionary<string, LinkedList<LoadCubeRequest>>();

        // Queue for requests that have material data but need model data
        private readonly Queue<LoadCubeRequest> _loadGeometryBufferQueue = new Queue<LoadCubeRequest>(10);

        // Queue for requests that have model and material data and so are ready for construction
        private readonly Queue<LoadCubeRequest> _buildCubeRequests = new Queue<LoadCubeRequest>(10);

        private static Thread _mainThread;

        protected bool Loaded { get; private set; }

        // Octree State Tracking
        private Dictionary<string, CubeTracker> cubeDict = new Dictionary<string, CubeTracker>();
        private LinkedList<CubeTracker> cubeList = new LinkedList<CubeTracker>();
        private Vector3 tempPosition;
        private PyriteCube cubeCamPos;
        private PyriteCube cubeCamPosNew;
        private PyriteQuery pyriteQuery;
        private PyriteSetVersionDetailLevel pyriteLevel;
        private PyriteSetVersionDetailLevel pyriteLevel1;
        private GameObject OctreeWorld;

        private void Start()
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

            InternalSetup();
            StartCoroutine(InternalLoad());
        }

        private IEnumerator InternalLoad()
        {
            yield return StartCoroutine(Load());
            Loaded = true;
        }

        private void InternalSetup()
        {
            _mainThread = Thread.CurrentThread;

            _guiStyle.normal.textColor = Color.red;


            if (_eboCache == null)
            {
                _eboCache = new DictionaryCache<string, GeometryBuffer>(EboCacheSize);
            }
            else
            {
                Debug.LogWarning("Ebo cache already initialized. Skipping initizliation.");
            }

            if (_materialDataCache == null)
            {
                _materialDataCache = new DictionaryCache<string, MaterialData>(MaterialDataCacheSize);
            }
            else
            {
                Debug.LogWarning("Material Data cache  already initialized. Skipping initizliation.");
            }

            if (_materialCache == null)
            {
                _materialCache = new DictionaryCache<string, Material>(MaterialCacheSize);
            }
            else
            {
                Debug.LogWarning("Material cache  already initialized. Skipping initizliation.");
            }

            ObjectPooler.Current.CreatePoolForObject(BaseModelCube);

            // Optional pool only used in camera detection scenario
            if (PlaceHolderCube != null)
            {
                ObjectPooler.Current.CreatePoolForObject(PlaceHolderCube);
            }

            CacheWebRequest.RehydrateCache(CacheSize);
        }

        private static bool CheckThread(bool expectMainThread)
        {
            var asExpected = expectMainThread != _mainThread.Equals(Thread.CurrentThread);
            if (asExpected)
            {
                Debug.LogWarning("Warning unexpected thread. Expected: " + expectMainThread);
            }
            return asExpected;
        }

        private static bool CheckIfMainThread()
        {
            return CheckThread(true);
        }

        private static bool CheckIfBackgroundThread()
        {
            return CheckThread(false);
        }

        private void Update()
        {
            if (!Loaded)
            {
                return;
            }

            // Update camera frustrum
            if(UseCameraDetection)
                CameraFrustrum = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            cubeCamPosNew = pyriteLevel1.GetCubeForWorldCoordinates(new Vector3(
                -CameraRig.transform.position.x,
                -CameraRig.transform.position.z,
                CameraRig.transform.position.y - _geometryBufferAltitudeTransform));          

            if (!cubeCamPos.Equals(cubeCamPosNew))
            {
                Debug.Log(String.Format("NEW CUBE POSITION: ({0},{1},{2})", cubeCamPosNew.X, cubeCamPosNew.Y, cubeCamPosNew.Z));
                cubeCamPos = cubeCamPosNew;
                LoadCamCubes();
            }

            // Check for work in Update
            ProcessQueues();
        }

        // Look through all work queues starting any work that is needed
        private void ProcessQueues()
        {
            // Look for requests that are ready to be constructed
            ProcessQueue(_buildCubeRequests, BuildCubeRequest);

            // Look for textures that have been downloaded and need to be converted to MaterialData
            if (Monitor.TryEnter(_texturesReadyForMaterialDataConstruction))
            {
                while (_texturesReadyForMaterialDataConstruction.Count > 0)
                {
                    var materialDataKeyTextureBytesPair = _texturesReadyForMaterialDataConstruction.Dequeue();
                    StartCoroutine(FinishCreatingMaterialDataWithTexture(materialDataKeyTextureBytesPair));
                }
                Monitor.Exit(_texturesReadyForMaterialDataConstruction);
            }
            // Look for requests that need material data set
            ProcessQueue(_loadMaterialQueue, GetMaterialForRequest);

            // Look for requests that need geometry buffer (model data)
            ProcessQueue(_loadGeometryBufferQueue, GetModelForRequest);
        }

        // Helper for locking a queue, pulling off requests and invoking a handler function for them
        private void ProcessQueue(Queue<LoadCubeRequest> queue, Func<LoadCubeRequest, IEnumerator> requestProcessFunc)
        {
            if (Monitor.TryEnter(queue))
            {
                while (queue.Count > 0)
                {
                    var request = queue.Dequeue();
                    if (!request.Cancelled)
                    {
                        StartCoroutine(requestProcessFunc(request));
                    }
                    else
                    {
                        CancelledRequests++;
                    }
                }
                Monitor.Exit(queue);
            }
        }

        /// <summary>
        /// Returns whether or not any requests are still active (not cancelled) for the provided dependency
        /// </summary>
        /// <param name="dependencyKey">Dependency we want to check for</param>
        /// <returns>true if any dependent requests are not cancelled, false if that is not the case</returns>
        public bool DependentRequestsExistBlocking(string dependencyKey)
        {
            CheckIfBackgroundThread();
            lock (_dependentCubes)
            {
                LinkedList<LoadCubeRequest> dependentRequests;
                if (_dependentCubes.TryGetValue(dependencyKey, out dependentRequests))
                {
                    if (dependentRequests.Any(request => !request.Cancelled))
                    {
                        return true;
                    }
                    // No dependent requests still active, delete the list
                    LateCancelledRequests++;
                    _dependentCubes.Remove(dependencyKey);
                }
                else
                {
                    Debug.LogError("Should not be possible...");
                }
            }
            return false;
        }

        private IEnumerator AddDependentRequest(LoadCubeRequest dependentRequest, string dependencyKey)
        {
            // Model is in the process of being constructed. Add request to dependency list
            while (!Monitor.TryEnter(_dependentCubes))
            {
                yield return null;
            }
            LinkedList<LoadCubeRequest> dependentRequests;
            if (!_dependentCubes.TryGetValue(dependencyKey, out dependentRequests))
            {
                dependentRequests = new LinkedList<LoadCubeRequest>();
                _dependentCubes.Add(dependencyKey, dependentRequests);
            }
            dependentRequests.AddLast(dependentRequest);
            Monitor.Exit(_dependentCubes);
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (ShowDebugText) // or check the app debug flag
            {
                if (EboCacheHits + EboCacheMisses > 1000)
                {
                    EboCacheMisses = EboCacheHits = 0;
                }

                if (MaterialCacheHits + MaterialCacheMisses > 1000)
                {
                    MaterialCacheMisses = MaterialCacheHits = 0;
                }

                var yOffset = 10;
                string caches;

                if (UseFileCache)
                {
                    caches = string.Format("Mesh {0}/{1} Mat {2}/{3} File {4}/{5} Cr {6} Lcr {7} Dr {8}",
                        EboCacheHits,
                        EboCacheMisses,
                        MaterialCacheHits,
                        MaterialCacheMisses,
                        FileCacheHits,
                        FileCacheMisses,
                        CancelledRequests,
                        LateCancelledRequests,
                        _dependentCubes.Count);
                }
                else
                {
                    caches = string.Format("Mesh {0}/{1} Mat {2}/{3} Cr {4} Lcr {5}",
                        EboCacheHits,
                        EboCacheMisses,
                        MaterialCacheHits,
                        MaterialCacheMisses,
                        CancelledRequests,
                        LateCancelledRequests);
                }

                GUI.Label(new Rect(10, yOffset, 200, 50), caches, _guiStyle);
            }
        }
#endif

        private PyriteCube CreateCubeFromCubeBounds(CubeBounds cubeBounds)
        {
            return new PyriteCube
            {
                X = (int) cubeBounds.BoundingBox.Min.x,
                Y = (int) cubeBounds.BoundingBox.Min.y,
                Z = (int) cubeBounds.BoundingBox.Min.z
            };
        }

        protected virtual IEnumerator Load()
        {
            pyriteQuery = new PyriteQuery(this, SetName, ModelVersion, PyriteServer, UpgradeFactor, UpgradeConstant,
                DowngradeFactor, DowngradeConstant);
            yield return StartCoroutine(pyriteQuery.LoadAll(FilterDetailLevels ? DetailLevelsToFilter : null));
            var initialDetailLevelIndex = DetailLevel - 1;
            if (UseCameraDetection)
            {
                initialDetailLevelIndex = pyriteQuery.DetailLevels.Length - 1;
            }

            pyriteLevel1 = pyriteQuery.DetailLevels[0];
            pyriteLevel = pyriteQuery.DetailLevels[initialDetailLevelIndex];

            if (UseOctreeSelection)
            {
                OctreeWorld = new GameObject("OctreeWorld");
                OctreeWorld.transform.position = Vector3.zero;
                OctreeWorld.transform.rotation = Quaternion.identity;
            }

            var allOctCubes = pyriteQuery.DetailLevels[initialDetailLevelIndex].Octree.AllItems();
            foreach (var octCube in allOctCubes)
            {
                var pCube = CreateCubeFromCubeBounds(octCube);
                var x = pCube.X;
                var y = pCube.Y;
                var z = pCube.Z;
                var cubePos = pyriteLevel.GetWorldCoordinatesForCube(pCube);
                _geometryBufferAltitudeTransform = 0 - pyriteLevel.ModelBoundsMin.z;

                if (UseCameraDetection)
                {
                    var detectionCube = ObjectPooler.Current.GetPooledObject(PlaceHolderCube);
                    detectionCube.transform.position = new Vector3(-cubePos.x,
                        cubePos.z + _geometryBufferAltitudeTransform, -cubePos.y);
                    detectionCube.transform.rotation = Quaternion.identity;
                    var meshRenderer = detectionCube.GetComponent<MeshRenderer>();
                    meshRenderer.enabled = true;
                    detectionCube.GetComponent<IsRendered>()
                        .SetCubePosition(x, y, z, initialDetailLevelIndex, pyriteQuery, this);

                    detectionCube.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);

                    detectionCube.SetActive(true);
                }
                else if (!UseOctreeSelection)
                {
                    var loadRequest = new LoadCubeRequest(x, y, z, initialDetailLevelIndex, pyriteQuery, null);
                    yield return StartCoroutine(EnqueueLoadCubeRequest(loadRequest));
                }

                //if (UseOctreeSelection)
                //{
                //    var adjustedPos = new Vector3(-cubePos.x, cubePos.z + _geometryBufferAltitudeTransform, -cubePos.y);
                //    var loc = Instantiate(OctreeTranslucenttCube, adjustedPos, Quaternion.identity) as GameObject;
                //    loc.name = string.Format("Mesh:{0},{1},{2}", pCube.X, pCube.Y, pCube.Z);
                //    loc.transform.localScale = new Vector3(
                //          pyriteLevel.WorldCubeScale.x,
                //          pyriteLevel.WorldCubeScale.z,
                //          pyriteLevel.WorldCubeScale.y);
                //    loc.transform.parent = OctreeWorld.transform;
                //}
            }
            
            if (CameraRig != null)
            {
                // Hardcodes the coordinate inversions which are parameterized on the geometry buffer

                var min = new Vector3(
                    -pyriteLevel.ModelBoundsMin.x,
                    pyriteLevel.ModelBoundsMin.z + _geometryBufferAltitudeTransform,
                    -pyriteLevel.ModelBoundsMin.y);
                var max = new Vector3(
                    -pyriteLevel.ModelBoundsMax.x,
                    pyriteLevel.ModelBoundsMax.z + _geometryBufferAltitudeTransform,
                    -pyriteLevel.ModelBoundsMax.y);

                var newCameraPosition = min + (max - min)/2.0f;                
                newCameraPosition += new Vector3(0, (max - min).y*1.4f, 0);                
                CameraRig.transform.position = newCameraPosition;
                CameraRig.transform.rotation = Quaternion.Euler(0, 180, 0);
            }

            cubeCamPos = pyriteLevel1.GetCubeForWorldCoordinates(new Vector3(
                -CameraRig.transform.position.x,
                -CameraRig.transform.position.z,
                CameraRig.transform.position.y - _geometryBufferAltitudeTransform));
            LoadCamCubes();
        }

        void LoadCamCubes()
        {
            //int detailLevel = DetailLevel - 1;
            for (int detailLevel = pyriteQuery.DetailLevels.Length - 1; detailLevel >= 0; detailLevel--)            
            {
                var pLevel = pyriteQuery.DetailLevels[detailLevel];

                var cPos = pLevel.GetCubeForWorldCoordinates(new Vector3(
                    -CameraRig.transform.position.x,
                    -CameraRig.transform.position.z,
                    CameraRig.transform.position.y - _geometryBufferAltitudeTransform));

                Debug.Log(String.Format("LoadCamCubes: ({0},{1},{2})", cPos.X, cPos.Y, cPos.Z));
                var cubeCamVector = new Vector3(cPos.X + 0.5f, cPos.Y + 0.5f, cPos.Z + 0.5f);
                var minVector = cubeCamVector - Vector3.one;
                var maxVector = cubeCamVector + Vector3.one;
                var octIntCubes = pLevel.Octree.AllIntersections(new BoundingBox(minVector, maxVector));

                int cubeCounter = 0;
                foreach (var i in octIntCubes)
                {
                    cubeCounter++;
                    var pCube = CreateCubeFromCubeBounds(i.Object);
                    var cubePos = pLevel.GetWorldCoordinatesForCube(pCube);
                    var cubeKey = string.Format("{0},{1}", detailLevel, pCube.GetKey());

                    // Setup object at cube location
                    if (cubeDict.ContainsKey(cubeKey))
                    {
                        var cube = cubeDict[cubeKey];
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

                        var adjustedPos = new Vector3(-cubePos.x, cubePos.z + _geometryBufferAltitudeTransform, -cubePos.y);
                        var gObj = Instantiate(OctreeMarkerCube, adjustedPos, Quaternion.identity) as GameObject;
                        gObj.transform.localScale = new Vector3(
                            pLevel.WorldCubeScale.x * .1f,
                            pLevel.WorldCubeScale.y * .1f,
                            pLevel.WorldCubeScale.z * .1f);

                        var loadRequest = new LoadCubeRequest(
                            pCube.X,
                            pCube.Y,
                            pCube.Z,
                            detailLevel, pyriteQuery, null);
                        StartCoroutine(EnqueueLoadCubeRequest(loadRequest));

                        if (cubeList.Count < MaxListCount)
                        {
                            ct = new CubeTracker(cubeKey, null);
                        }
                        else
                        {
                            // Reuse Last CubeTracker
                            Debug.Log("Reusing Cube");
                            ct = cubeList.Last.Value;
                            cubeList.RemoveLast();
                            cubeDict.Remove(ct.DictKey);
                            ct.DictKey = cubeKey;

                            // TODO: Reassign GameObject Content instead of destroying
                            Destroy(ct.gameObject);

                            if (ct.Active)
                            {
                                Debug.Log("ALERT: Active Object in List Tail");
                            }
                        }
                        gObj.transform.parent = gameObject.transform;
                        ct.gameObject = gObj;
                        ct.pyriteQuery = pyriteQuery;
                        ct.Active = true;
                        cubeList.AddFirst(ct);
                        cubeDict.Add(ct.DictKey, ct);
                    }
                }
                Debug.Log(String.Format("CubeCounter: {0}  CubeList/Dict: {1}/{2}", cubeCounter, cubeList.Count,
                    cubeDict.Count));

                foreach (var q in cubeList.Skip(cubeCounter).TakeWhile(q => q.Active))
                {                    
                    if(q.l == detailLevel)
                        q.Active = false;
                }
            }
        }

        public IEnumerator AddUpgradedDetectorCubes(PyriteQuery pyriteQuery, int x, int y, int z, int lod,
            Action<IEnumerable<GameObject>> registerCreatedDetectorCubes)
        {
            var newLod = lod - 1;
            var createdDetectors = new List<GameObject>();
            var pyriteLevel = pyriteQuery.DetailLevels[newLod];

            var cubeFactor = pyriteQuery.GetNextCubeFactor(lod);
            var min = new Vector3(x*(int) cubeFactor.x + 0.5f, y*(int) cubeFactor.y + 0.5f, z*(int) cubeFactor.z + 0.5f);
            var max = new Vector3((x + 1)*(int) cubeFactor.x - 0.5f, (y + 1)*(int) cubeFactor.y - 0.5f,
                (z + 1)*(int) cubeFactor.z - 0.5f);
            var intersections =
                pyriteQuery.DetailLevels[newLod].Octree.AllIntersections(new BoundingBox {Min = min, Max = max});
            foreach (var i in intersections)
            {
                var newCube = CreateCubeFromCubeBounds(i.Object);
                var cubePos = pyriteLevel.GetWorldCoordinatesForCube(newCube);

                var newDetectionCube = ObjectPooler.Current.GetPooledObject(PlaceHolderCube);
                newDetectionCube.transform.position = new Vector3(-cubePos.x,
                    cubePos.z + _geometryBufferAltitudeTransform, -cubePos.y);
                newDetectionCube.transform.rotation = Quaternion.identity;
                var meshRenderer = newDetectionCube.GetComponent<MeshRenderer>();
                meshRenderer.enabled = true;
                newDetectionCube.GetComponent<IsRendered>()
                    .SetCubePosition(newCube.X, newCube.Y, newCube.Z, newLod, pyriteQuery, this);

                newDetectionCube.transform.localScale = new Vector3(
                    pyriteLevel.WorldCubeScale.x,
                    pyriteLevel.WorldCubeScale.z,
                    pyriteLevel.WorldCubeScale.y);
                newDetectionCube.SetActive(true);
                createdDetectors.Add(newDetectionCube);
            }
            registerCreatedDetectorCubes(createdDetectors);
            yield break;
        }

        // Used to initiate request to load and display a cube in the scene
        public IEnumerator EnqueueLoadCubeRequest(LoadCubeRequest loadRequest)
        {
            yield return StartCoroutine(_loadMaterialQueue.ConcurrentEnqueue(loadRequest));
        }

        // Invoked when a load requeset has failed to download the model data it needs
        // Requests are retried RETRY_LIMIT times if they fail more than that the request is abandoned (error is logged)
        // When this happens if any dependent cubes want the resource that failed one request is re-queued to try again (under that requests Retry quota)
        private void FailGetGeometryBufferRequest(LoadCubeRequest loadRequest, string modelPath)
        {
            CheckIfBackgroundThread();
            loadRequest.Failures++;
            lock (_eboCache)
            {
                // Remove the 'in progress' marker from the cache
                _eboCache.Remove(modelPath);
            }

            if (RETRY_LIMIT > loadRequest.Failures)
            {
                Debug.LogError("Retry limit hit for: " + modelPath);
                Debug.LogError("Cube load failed for " + loadRequest);

                // Let another depenent cube try
                lock (_dependentCubes)
                {
                    LinkedList<LoadCubeRequest> dependentRequests;
                    if (_dependentCubes.TryGetValue(modelPath, out dependentRequests))
                    {
                        var request = dependentRequests.Last.Value;
                        dependentRequests.RemoveLast();
                        _loadGeometryBufferQueue.ConcurrentEnqueue(request).Wait();
                    }
                }
            }
            else
            {
                // Queue for retry
                _loadGeometryBufferQueue.ConcurrentEnqueue(loadRequest).Wait();
            }
        }

        // Invoked when a load requeset has failed to download the material data it needs
        // Requests are retried RETRY_LIMIT times if they fail more than that the request is abandoned (error is logged)
        // When this happens if any dependent cubes want the resource that failed one request is re-queued to try again (under that request's Retry quota)
        private void FailGetMaterialDataRequest(LoadCubeRequest loadRequest, string materialPath)
        {
            CheckIfBackgroundThread();
            loadRequest.Failures++;
            lock (_materialDataCache)
            {
                // Remove the 'in progress' marker from the cache
                _materialDataCache.Remove(materialPath);
            }


            if (RETRY_LIMIT > loadRequest.Failures)
            {
                Debug.LogError("Retry limit hit for: " + materialPath);
                Debug.LogError("Cube load failed for " + loadRequest);

                lock (_dependentCubes)
                {
                    LinkedList<LoadCubeRequest> dependentRequests;
                    if (_dependentCubes.TryGetValue(materialPath, out dependentRequests))
                    {
                        var request = dependentRequests.Last.Value;
                        dependentRequests.RemoveLast();
                        _loadMaterialQueue.ConcurrentEnqueue(request).Wait();
                    }
                }
            }
            else
            {
                // Queue for retry
                _loadMaterialQueue.ConcurrentEnqueue(loadRequest).Wait();
            }
        }

        private IEnumerator SucceedGetGeometryBufferRequest(string modelPath)
        {
            // Check to see if any other requests were waiting on this model
            LinkedList<LoadCubeRequest> dependentRequests;
            while (!Monitor.TryEnter(_dependentCubes))
            {
                yield return null;
            }
            if (_dependentCubes.TryGetValue(modelPath, out dependentRequests))
            {
                _dependentCubes.Remove(modelPath);
            }
            Monitor.Exit(_dependentCubes);

            // If any were send them to their next stage
            if (dependentRequests != null)
            {
                foreach (var request in dependentRequests)
                {
                    request.GeometryBuffer = _eboCache[modelPath];
                    MoveRequestForward(request);
                }
            }
        }

        // Called when the material data has been constructed into the cache
        // The material data is constructed using a materialkey for reference
        // The method sets the material data for any dependent requests and moves them along
        private IEnumerator SucceedGetMaterialDataRequests(string materialDataKey)
        {
            CheckIfMainThread();
            // Check to see if any other requests were waiting on this model
            LinkedList<LoadCubeRequest> dependentRequests;
            while (!Monitor.TryEnter(_dependentCubes))
            {
                yield return null;
            }
            if (_dependentCubes.TryGetValue(materialDataKey, out dependentRequests))
            {
                _dependentCubes.Remove(materialDataKey);
            }
            Monitor.Exit(_dependentCubes);

            // If any were send them to their next stage
            if (dependentRequests != null)
            {
                foreach (var request in dependentRequests)
                {
                    request.MaterialData = _materialDataCache[materialDataKey];
                    MoveRequestForward(request);
                }
            }
        }

        // Determine the next appropriate queue for the request
        private void MoveRequestForward(LoadCubeRequest loadRequest)
        {
            var onMainThread = _mainThread.Equals(Thread.CurrentThread);
            if (loadRequest.GeometryBuffer == null)
            {
                if (onMainThread)
                {
                    StartCoroutine(_loadGeometryBufferQueue.ConcurrentEnqueue(loadRequest));
                }
                else
                {
                    _loadGeometryBufferQueue.ConcurrentEnqueue(loadRequest).Wait();
                }
            }
            else if (loadRequest.MaterialData == null)
            {
                if (onMainThread)
                {
                    StartCoroutine(_loadMaterialQueue.ConcurrentEnqueue(loadRequest));
                }
                else
                {
                    _loadMaterialQueue.ConcurrentEnqueue(loadRequest).Wait();
                }
            }
            else
            {
                if (onMainThread)
                {
                    StartCoroutine(_buildCubeRequests.ConcurrentEnqueue(loadRequest));
                }
                else
                {
                    _buildCubeRequests.ConcurrentEnqueue(loadRequest).Wait();
                }
            }
        }

        // Responsible for getting the geometry data for a given request
        // The method works roughly as follows
        // 1. Check if model data is in cache
        //    a. If not, start a web request for the data and add this request to the dependency list for the path
        // 2. If the model data cache indicates that it is being filled (a set value of null) (including if the request
        //      just started during this invocation) add the request to the dependency list for this path 
        // 3. If the model is in the cache and set then get the data for the request and move it forward
        private IEnumerator GetModelForRequest(LoadCubeRequest loadRequest)
        {
            var modelPath = loadRequest.Query.GetModelPath(loadRequest.LodIndex, loadRequest.X, loadRequest.Y,
                loadRequest.Z);

            // If the geometry data is being loaded or this is the first request to load it add the request the dependency list
            if (!_eboCache.ContainsKey(modelPath) || _eboCache[modelPath] == null)
            {
                yield return StartCoroutine(AddDependentRequest(loadRequest, modelPath));

                if (!_eboCache.ContainsKey(modelPath))
                {
                    // Model data was not present in cache nor has any request started constructing it
                    EboCacheMisses++;

                    _eboCache[modelPath] = null;
                    if (UseWwwForEbo)
                    {
                        var cachePath = CacheWebRequest.GetCacheFilePath(modelPath);
                        WWW modelWww;
                        if (CacheWebRequest.IsItemInCache(cachePath))
                        {
                            FileCacheHits++;
                            modelWww = new WWW("file:///" + cachePath);
                            yield return modelWww;
                        }
                        else
                        {
                            FileCacheMisses++;
                            modelWww = new WWW(modelPath);
                            yield return modelWww;
                            CacheWebRequest.AddToCache(cachePath, modelWww.bytes);
                        }

                        _eboCache[modelPath] =
                            new GeometryBuffer(_geometryBufferAltitudeTransform, true)
                            {
                                Buffer = modelWww.bytes
                            };
                        yield return StartCoroutine(SucceedGetGeometryBufferRequest(modelPath));
                    }
                    else
                    {
                        CacheWebRequest.GetBytes(modelPath, modelResponse =>
                        {
                            if (modelResponse.Status == CacheWebRequest.CacheWebResponseStatus.Error)
                            {
                                Debug.LogError("Error getting model [" + modelPath + "] " + modelResponse.ErrorMessage);
                                FailGetGeometryBufferRequest(loadRequest, modelPath);
                            }
                            else if (modelResponse.Status == CacheWebRequest.CacheWebResponseStatus.Cancelled)
                            {
                                _eboCache.Remove(modelPath);
                            }
                            else
                            {
                                if (modelResponse.IsCacheHit)
                                {
                                    FileCacheHits++;
                                }
                                else
                                {
                                    FileCacheMisses++;
                                }
                                _eboCache[modelPath] = new GeometryBuffer(_geometryBufferAltitudeTransform, true)
                                {
                                    Buffer = modelResponse.Content
                                };
                                SucceedGetGeometryBufferRequest(modelPath).Wait();
                            }
                        }, DependentRequestsExistBlocking);
                    }
                }
            }
            else // The model data was in the cache
            {
                // Model was constructed move request to next step
                EboCacheHits++;
                loadRequest.GeometryBuffer = _eboCache[modelPath];
                MoveRequestForward(loadRequest);
            }
        }

        // Responsible for getting the material data for a load request
        // The method works roughly as follows
        // 1. Check if material data is in cache
        //    a. If not, start a web request for the data and add this request to the dependency list for the path
        // 2. If the material data cache indicates that it is being filled (a set value of null) (including if the 
        //      request just started during this invocation) add the request to the dependency list for this path 
        // 3. If the material is in the cache and set then get the data for the request and move it forward
        private IEnumerator GetMaterialForRequest(LoadCubeRequest loadRequest)
        {
            var pyriteLevel =
                loadRequest.Query.DetailLevels[loadRequest.LodIndex];
            var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(loadRequest.X, loadRequest.Y);
            var texturePath = loadRequest.Query.GetTexturePath(loadRequest.LodIndex,
                (int) textureCoordinates.x,
                (int) textureCoordinates.y);

            // If the material data is not in the cache or in the middle of being constructed add this request as a dependency
            if (!_materialDataCache.ContainsKey(texturePath) || _materialDataCache[texturePath] == null)
            {
                // Add this requst to list of requests that is waiting for the data
                yield return StartCoroutine(AddDependentRequest(loadRequest, texturePath));

                // Check if this is the first request for material (or it isn't in the cache)
                if (!_materialDataCache.ContainsKey(texturePath))
                {
                    if (UseWwwForTextures)
                    {
                        // Material data was not in cache nor being constructed 
                        // Cache counter
                        MaterialCacheMisses++;
                        // Set to null to signal to other tasks that the key is in the process
                        // of being filled
                        _materialDataCache[texturePath] = null;
                        var materialData = CubeBuilderHelpers.GetDefaultMaterialData((int) textureCoordinates.x,
                            (int) textureCoordinates.y, loadRequest.LodIndex);
                        var cachePath = CacheWebRequest.GetCacheFilePath(texturePath);
                        if (!CacheFill)
                        {
                            WWW textureWww; // = new WWW(texturePath);
                            if (CacheWebRequest.IsItemInCache(cachePath))
                            {
                                FileCacheHits++;
                                textureWww = new WWW("file:///" + cachePath);
                                yield return textureWww;
                            }
                            else
                            {
                                FileCacheMisses++;
                                textureWww = new WWW(texturePath);
                                yield return textureWww;
                                CacheWebRequest.AddToCache(cachePath, textureWww.bytes);
                            }

                            materialData.DiffuseTex = textureWww.texture;
                        }
                        _materialDataCache[texturePath] = materialData;

                        // Move forward dependent requests that wanted this material data
                        yield return StartCoroutine(SucceedGetMaterialDataRequests(texturePath));
                    }
                    else
                    {
                        // Material data was not in cache nor being constructed 
                        // Cache counter
                        MaterialCacheMisses++;
                        // Set to null to signal to other tasks that the key is in the process
                        // of being filled
                        _materialDataCache[texturePath] = null;
                        var materialData = CubeBuilderHelpers.GetDefaultMaterialData((int) textureCoordinates.x,
                            (int) textureCoordinates.y, loadRequest.LodIndex);
                        _partiallyConstructedMaterialDatas[texturePath] = materialData;

                        CacheWebRequest.GetBytes(texturePath, textureResponse =>
                        {
                            CheckIfBackgroundThread();
                            if (textureResponse.Status == CacheWebRequest.CacheWebResponseStatus.Error)
                            {
                                Debug.LogError("Error getting texture [" + texturePath + "] " +
                                               textureResponse.ErrorMessage);
                                FailGetMaterialDataRequest(loadRequest, texturePath);
                            }
                            else if (textureResponse.Status == CacheWebRequest.CacheWebResponseStatus.Cancelled)
                            {
                                _materialDataCache.Remove(texturePath);
                            }
                            else
                            {
                                if (textureResponse.IsCacheHit)
                                {
                                    FileCacheHits++;
                                }
                                else
                                {
                                    FileCacheMisses++;
                                }
                                _texturesReadyForMaterialDataConstruction.ConcurrentEnqueue(
                                    new KeyValuePair<string, byte[]>(texturePath, textureResponse.Content)).Wait();
                            }
                        }, DependentRequestsExistBlocking);
                    }
                }
            }
            else // The material was in the cache
            {
                while (!Monitor.TryEnter(_materialDataCache))
                {
                    yield return null;
                }
                // Material data ready get it and move on
                MaterialCacheHits++;
                loadRequest.MaterialData = _materialDataCache[texturePath];
                MoveRequestForward(loadRequest);
                Monitor.Exit(_materialDataCache);
            }
        }

        // Used to create material data when a texture has finished downloading
        private IEnumerator FinishCreatingMaterialDataWithTexture(
            KeyValuePair<string, byte[]> materialDataKeyAndTexturePair)
        {
            var materialDataKey = materialDataKeyAndTexturePair.Key;
            while (!Monitor.TryEnter(_partiallyConstructedMaterialDatas))
            {
                yield return null;
            }
            var inProgressMaterialData = _partiallyConstructedMaterialDatas[materialDataKey];
            _partiallyConstructedMaterialDatas.Remove(materialDataKey);
            Monitor.Exit(_partiallyConstructedMaterialDatas);
            if (!CacheFill)
            {
                var texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                texture.LoadImage(materialDataKeyAndTexturePair.Value);


                inProgressMaterialData.DiffuseTex = texture;
            }

            _materialDataCache[materialDataKey] = inProgressMaterialData;

            // Move forward dependent requests that wanted this material data
            yield return StartCoroutine(SucceedGetMaterialDataRequests(materialDataKey));
        }

        // Used to create a and populate a game object for this request 
        private IEnumerator BuildCubeRequest(LoadCubeRequest loadRequest)
        {
            Build(loadRequest.GeometryBuffer, loadRequest.MaterialData, loadRequest.X, loadRequest.Y, loadRequest.Z,
                loadRequest.Query.DetailLevels[loadRequest.LodIndex].Value, loadRequest.RegisterCreatedObjects);
            yield break;
        }

        private void Build(GeometryBuffer buffer, MaterialData materialData, int x, int y, int z, int lod,
            Action<GameObject> registerCreatedObjects)
        {
            if (!_materialCache.ContainsKey(materialData.Name))
            {
                _materialCache[materialData.Name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, materialData);
            }

            if (CacheFill)
            {
                return;
            }

            var cubeName = new StringBuilder("cube_L");
            cubeName.Append(lod);
            cubeName.Append(':');
            cubeName.Append(x);
            cubeName.Append('_');
            cubeName.Append(y);
            cubeName.Append('_');
            cubeName.Append(z);

            var newCube = ObjectPooler.Current.GetPooledObject(BaseModelCube);
            newCube.name = cubeName.ToString();

            buffer.PopulateMeshes(newCube, _materialCache[materialData.Name]);

            // Put object in scene, claim from pool
            newCube.SetActive(true);

            if (registerCreatedObjects != null)
            {
                registerCreatedObjects(newCube);
            }
        }
    }
}