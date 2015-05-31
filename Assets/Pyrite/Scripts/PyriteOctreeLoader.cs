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
        public bool UseWwwForTextures = false;
        public bool UseWwwForEbo = false;
        public bool CacheFill = false;
        public int CacheSize = 3000;
        public bool UseUnlitShader = true;

        [Header("Octree Options")]
        public bool ShowDebugCubes = false;
        public bool ShowLocatorCubes = false;
        public bool ShowCubes = false;
        public int OctreeListCount = 50;
        public float BoundBoxSize = 2.0f;
        public GameObject RenderCube;
        public GameObject TranslucentCube;
        public GameObject LocatorCube;

        [Header("Server Data Options")]
        public static int MaterialCacheSize = 100;
        public static int MaterialDataCacheSize = 100;        
        public static int EboCacheSize = 250;
        
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

        // Processing Objects
        private const int RETRY_LIMIT = 2;
        private float _geometryBufferAltitudeTransform = 0;

        private static Thread _mainThread;
        private readonly Queue<LoadCubeRequest> _loadMaterialQueue = new Queue<LoadCubeRequest>(10);
        private readonly DictionaryCache<string, MaterialData> _materialDataCache = new DictionaryCache<string, MaterialData>(MaterialDataCacheSize);
        private readonly Dictionary<string, LinkedList<LoadCubeRequest>> _dependentCubes = new Dictionary<string, LinkedList<LoadCubeRequest>>();
        private readonly Queue<LoadCubeRequest> _loadGeometryBufferQueue = new Queue<LoadCubeRequest>(10);
        private readonly Dictionary<string, MaterialData> _partiallyConstructedMaterialDatas = new Dictionary<string, MaterialData>();
        private readonly Queue<KeyValuePair<string, byte[]>> _texturesReadyForMaterialDataConstruction = new Queue<KeyValuePair<string, byte[]>>(5);
        private readonly Queue<LoadCubeRequest> _buildCubeRequests = new Queue<LoadCubeRequest>(10);
        private readonly DictionaryCache<string, GeometryBuffer> _eboCache = new DictionaryCache<string, GeometryBuffer>(EboCacheSize);
        private readonly DictionaryCache<string, Material> _materialCache = new DictionaryCache<string, Material>(MaterialCacheSize);
        
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

                ProcessQueues();
            }
        }

        private IEnumerator InternalLoad()
        {
            _mainThread = Thread.CurrentThread;
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
            // TODO: Render multiple levels with cube collapsing
            //for (int detailLevel = pyriteQuery.DetailLevels.Length - 1; detailLevel >= 0; detailLevel--)            
            {
                var detailLevel = DetailLevel;  // Render specific detail level within Octree Selection

                var pLevel = pyriteQuery.DetailLevels[detailLevel];                
                var cPos = pLevel.GetCubeForWorldCoordinates(new Vector3(
                    -CameraRig.transform.position.x,
                    -CameraRig.transform.position.z,
                    CameraRig.transform.position.y - WorldYOffset));

                Debug.Log(String.Format("LoadCamCubes: ({0},{1},{2})", cPos.X, cPos.Y, cPos.Z));
                var cubeCamVector = new Vector3(cPos.X + 0.5f, cPos.Y + 0.5f, cPos.Z + 0.5f);
                
                var BoxSize = new Vector3(BoundBoxSize, BoundBoxSize, BoundBoxSize);
                var minVector = cubeCamVector - BoxSize;
                var maxVector = cubeCamVector + BoxSize;


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
                            ct = new CubeTracker(cubeKey)
                            {
                                gameObject = Instantiate(RenderCube, new Vector3(0, WorldYOffset, 0), Quaternion.identity) as GameObject
                            };
                        }
                        else
                        {
                            // Reuse Last CubeTracker
                            Debug.Log("Reusing Cube");
                            ct = cubeList.Last.Value;
                            if (ct.Active)
                            {
                                Debug.LogError("ERROR: Active Object in List Tail. Too many required cubes.");
                                return;
                            }
                            cubeList.RemoveLast();
                            cubeDict.Remove(ct.DictKey);
                            ct.DictKey = cubeKey;

                            if (ct.gameObject != null)
                            {
                                ct.gameObject.name = cubeKey;
                                ct.ClearMesh();                                
                            }
                        }

                        // TODO: Load Mesh for Cube Position
                        Debug.Log("Request Cube");
                        RequestCube(pCube.X, pCube.Y, pCube.Z, detailLevel, ct.gameObject);

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
                            cube.Active = false;                           

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

        void RequestCube(int X, int Y, int Z, int detailLevel, GameObject refObj)
        {
            //var loadRequest = new LoadCubeRequest(X, Y, Z, detailLevel, pyriteQuery, cubeObj => { refObj = cubeObj; });
            var loadRequest = new LoadCubeRequest(X, Y, Z, detailLevel, refObj);
            StartCoroutine(EnqueueLoadCubeRequest(loadRequest));
        }

        public IEnumerator EnqueueLoadCubeRequest(LoadCubeRequest loadRequest)
        {
            yield return StartCoroutine(_loadMaterialQueue.ConcurrentEnqueue(loadRequest));
        }

        private void ProcessQueues()
        {
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

            ProcessQueue(_loadMaterialQueue, GetMaterialForRequest);
            ProcessQueue(_loadGeometryBufferQueue, GetModelForRequest);            
        }

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
                }
                Monitor.Exit(queue);
            }
        }

        private IEnumerator GetMaterialForRequest(LoadCubeRequest loadRequest)
        {
            //var pyriteLevel = loadRequest.Query.DetailLevels[loadRequest.LodIndex];
            //var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(loadRequest.X, loadRequest.Y);
            //var texturePath = loadRequest.Query.GetTexturePath(loadRequest.LodIndex, (int)textureCoordinates.x, (int)textureCoordinates.y);

            var pyriteLevel = pyriteQuery.DetailLevels[loadRequest.LodIndex];
            var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(loadRequest.X, loadRequest.Y);
            var texturePath = pyriteQuery.GetTexturePath(loadRequest.LodIndex, (int)textureCoordinates.x, (int)textureCoordinates.y);

            // If the material data is not in the cache or in the middle of being constructed add this request as a dependency
            if (!_materialDataCache.ContainsKey(texturePath) || _materialDataCache[texturePath] == null)
            {
                // Add this request to list of requests that is waiting for the data
                yield return StartCoroutine(AddDependentRequest(loadRequest, texturePath));

                // Check if this is the first request for material (or it isn't in the cache)
                if (!_materialDataCache.ContainsKey(texturePath))
                {
                    if (UseWwwForTextures)
                    {
                        // Material data was not in cache nor being constructed                                                 
                        // Set to null to signal to other tasks that the key is in the process
                        // of being filled
                        _materialDataCache[texturePath] = null;
                        var materialData = CubeBuilderHelpers.GetDefaultMaterialData(
                            (int) textureCoordinates.x,
                            (int) textureCoordinates.y,
                            loadRequest.LodIndex);
                        var cachePath = CacheWebRequest.GetCacheFilePath(texturePath);
                        if (!CacheFill)
                        {
                            WWW textureWww; // = new WWW(texturePath);
                            if (CacheWebRequest.IsItemInCache(cachePath))
                            {                                
                                textureWww = new WWW("file:///" + cachePath);
                                yield return textureWww;
                            }
                            else
                            {                                
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
                        // Set to null to signal to other tasks that the key is in the process
                        // of being filled
                        _materialDataCache[texturePath] = null;
                        var materialData = CubeBuilderHelpers.GetDefaultMaterialData(
                            (int) textureCoordinates.x,
                            (int) textureCoordinates.y,
                            loadRequest.LodIndex);
                        _partiallyConstructedMaterialDatas[texturePath] = materialData;

                        CacheWebRequest.GetBytes(texturePath, textureResponse =>
                        {
                            CheckIfBackgroundThread();
                            if (textureResponse.Status == CacheWebRequest.CacheWebResponseStatus.Error)
                            {
                                Debug.LogError("Error getting texture [" + texturePath + "] " + textureResponse.ErrorMessage);
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
                                    //FileCacheHits++;
                                }
                                else
                                {
                                    //FileCacheMisses++;
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
                //MaterialCacheHits++;
                loadRequest.MaterialData = _materialDataCache[texturePath];
                MoveRequestForward(loadRequest);
                Monitor.Exit(_materialDataCache);
            }
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

        private static bool CheckIfMainThread()
        {
            return CheckThread(true);
        }

        private static bool CheckIfBackgroundThread()
        {
            return CheckThread(false);
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
                    //LateCancelledRequests++;
                    _dependentCubes.Remove(dependencyKey);
                }
                else
                {
                    Debug.LogError("DependentRequestsExistBlocking: Should not be possible");
                }
            }
            return false;
        }

        private IEnumerator GetModelForRequest(LoadCubeRequest loadRequest)
        {
            //var modelPath = loadRequest.Query.GetModelPath(loadRequest.LodIndex, loadRequest.X, loadRequest.Y, loadRequest.Z);
            var modelPath = pyriteQuery.GetModelPath(loadRequest.LodIndex, loadRequest.X, loadRequest.Y, loadRequest.Z);

            // If the geometry data is being loaded or this is the first request to load it add the request the dependency list
            if (!_eboCache.ContainsKey(modelPath) || _eboCache[modelPath] == null)
            {
                yield return StartCoroutine(AddDependentRequest(loadRequest, modelPath));

                if (!_eboCache.ContainsKey(modelPath))
                {
                    // Model data was not present in cache nor has any request started constructing it
                    //EboCacheMisses++;

                    _eboCache[modelPath] = null;
                    if (UseWwwForEbo)
                    {
                        var cachePath = CacheWebRequest.GetCacheFilePath(modelPath);
                        WWW modelWww;
                        if (CacheWebRequest.IsItemInCache(cachePath))
                        {
                            //FileCacheHits++;
                            modelWww = new WWW("file:///" + cachePath);
                            yield return modelWww;
                        }
                        else
                        {
                            //FileCacheMisses++;
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
                                    //FileCacheHits++;
                                }
                                else
                                {
                                    //FileCacheMisses++;
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
                //EboCacheHits++;
                loadRequest.GeometryBuffer = _eboCache[modelPath];
                MoveRequestForward(loadRequest);
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

        private IEnumerator BuildCubeRequest(LoadCubeRequest loadRequest)
        {
            Build(
                loadRequest.GeometryBuffer,
                loadRequest.MaterialData,
                loadRequest.X,
                loadRequest.Y,
                loadRequest.Z,
                loadRequest.LodIndex,
                loadRequest.gameObject);
            yield break;
        }

        private void Build(GeometryBuffer buffer, MaterialData materialData, int x, int y, int z, int lod, GameObject obj)
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
            
            obj.name = cubeName.ToString();
            buffer.PopulateMeshes(obj, _materialCache[materialData.Name]);
            obj.SetActive(true);            
        }

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
    }
}