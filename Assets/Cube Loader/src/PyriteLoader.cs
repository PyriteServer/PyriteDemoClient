namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Microsoft.Xna.Framework;
    using Model;
    using UnityEngine;
    using Debug = UnityEngine.Debug;

    public class PyriteLoader : MonoBehaviour
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private readonly Color[] _colorList =
        {
            Color.gray, Color.yellow, Color.cyan
        };

        private readonly DictionaryCache<string, GeometryBuffer> _eboCache =
            new DictionaryCache<string, GeometryBuffer>(250);

        private readonly DictionaryCache<string, Material> _materialCache = new DictionaryCache<string, Material>(100);

        private readonly DictionaryCache<string, MaterialData> _materialDataCache =
            new DictionaryCache<string, MaterialData>(100);

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
        private int _colorSelector;       

        private float _geometryBufferAltitudeTransform;

        public GameObject CameraRig;

        public bool EnableDebugLogs = false;

        // Prefab for detection cubes
        public GameObject PlaceHolderCube;

        // Prefab for base cube object that we will populate data
        public GameObject BaseModelCube;

        [Header("Server Options")] public string PyriteServer;
        [Range(0, 100)] public int MaxConcurrentRequests = 8;

        [Header("Set Options (required)")] public int DetailLevel = 6;
        public string ModelVersion = "V2";
        public string SetName;

        [Header("Debug Options")] public bool UseCameraDetection = true;
        public bool UseUnlitShader = true;
        public bool UseFileCache = true;
        public bool ShowDebugText = true;

        [Header("Other Options")] public float UpgradeFactor = 1.05f;
        public float UpgradeConstant = 0.0f;
        public bool UseWwwForTextures = false;

        [HideInInspector()]
        public Plane[] CameraFrustrum = null;

        private readonly HashSet<string> _activeRequests = new HashSet<string>();

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

        protected void DebugLog(string fmt, params object[] args)
        {
            if (EnableDebugLogs)
            {
                var content = string.Format(fmt, args);
                Debug.LogFormat("{0}: {1}", _sw.ElapsedMilliseconds, content);
            }
        }

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

            _mainThread = Thread.CurrentThread;

            _guiStyle.normal.textColor = Color.red;

            ObjectPooler.Current.CreatePoolForObject(BaseModelCube);
            
            // Optional pool only used in camera detection scenario
            if (PlaceHolderCube != null)
            {
                ObjectPooler.Current.CreatePoolForObject(PlaceHolderCube);
            }         

            CacheWebRequest.RehydrateCache();

            DebugLog("+Start()");
            StartCoroutine(Load());
            DebugLog("-Start()");            
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
            // Update camera frustrum
            CameraFrustrum = GeometryUtility.CalculateFrustumPlanes(Camera.main);

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
                    if (dependentRequests.Any((request) => !request.Cancelled))
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

        public IEnumerator Load()
        {
            DebugLog("+Load()");

            var pyriteQuery = new PyriteQuery(this, SetName, ModelVersion, PyriteServer, UpgradeFactor, UpgradeConstant);
            yield return StartCoroutine(pyriteQuery.LoadAll());
            DebugLog("CubeQuery complete.");

            var pyriteLevel =
                pyriteQuery.DetailLevels[DetailLevel];

            var allOctCubes = pyriteQuery.DetailLevels[DetailLevel].Octree.AllItems();
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
                    meshRenderer.material.color =
                        _colorList[_colorSelector%_colorList.Length];
                    meshRenderer.enabled = true;
                    detectionCube.GetComponent<IsRendered>().SetCubePosition(x, y, z, DetailLevel, pyriteQuery, this);

                    detectionCube.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);

                    detectionCube.SetActive(true);

                    _colorSelector++;
                }
                else
                {
                    var loadRequest = new LoadCubeRequest(x, y, z, DetailLevel, pyriteQuery, null);
                    yield return StartCoroutine(EnqueueLoadCubeRequest(loadRequest));
                }
            }

            if (CameraRig != null)
            {
                DebugLog("Moving camera");
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

                DebugLog("Done moving camera");
            }
            DebugLog("-Load()");
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
                newDetectionCube.transform.position = new Vector3(-cubePos.x, cubePos.z + _geometryBufferAltitudeTransform, -cubePos.y);
                newDetectionCube.transform.rotation = Quaternion.identity;
                var meshRenderer = newDetectionCube.GetComponent<MeshRenderer>();
                meshRenderer.material.color =
                    _colorList[_colorSelector % _colorList.Length];
                meshRenderer.enabled = true;
                newDetectionCube.GetComponent<IsRendered>().SetCubePosition(newCube.X, newCube.Y, newCube.Z, newLod, pyriteQuery, this);

                newDetectionCube.transform.localScale = new Vector3(
                    pyriteLevel.WorldCubeScale.x,
                    pyriteLevel.WorldCubeScale.z,
                    pyriteLevel.WorldCubeScale.y);
                _colorSelector++;
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
            DebugLog("+LoadCube(L{3}:{0}_{1}_{2})", loadRequest.X, loadRequest.Y, loadRequest.Z, loadRequest.Lod);
            var modelPath = loadRequest.Query.GetModelPath(loadRequest.Lod, loadRequest.X, loadRequest.Y, loadRequest.Z);

            // If the geometry data is being loaded or this is the first request to load it add the request the dependency list
            if (!_eboCache.ContainsKey(modelPath) || _eboCache[modelPath] == null)
            {
                yield return StartCoroutine(AddDependentRequest(loadRequest, modelPath));

                if (!_eboCache.ContainsKey(modelPath))
                {
                    // Model data was not present in cache nor has any request started constructing it
                    EboCacheMisses++;

                    _eboCache[modelPath] = null;

                    CacheWebRequest.GetBytes(modelPath, modelResponse =>
                    {
                        if (modelResponse.Status == CacheWebRequest.CacheWebResponseStatus.Error)
                        {
                            Debug.LogError("Error getting model [" + modelPath + "] " + modelResponse.ErrorMessage);
                            FailGetGeometryBufferRequest(loadRequest, modelPath);
                        }
                        else if(modelResponse.Status == CacheWebRequest.CacheWebResponseStatus.Cancelled)
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
                loadRequest.Query.DetailLevels[loadRequest.Lod];
            var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(loadRequest.X, loadRequest.Y);
            var texturePath = loadRequest.Query.GetTexturePath(loadRequest.Lod,
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
                            (int) textureCoordinates.y, loadRequest.Lod);

                        WWW textureWww = new WWW(texturePath);
                        yield return textureWww;
                        materialData.DiffuseTex = textureWww.texture;
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
                        var materialData = CubeBuilderHelpers.GetDefaultMaterialData((int)textureCoordinates.x,
                            (int)textureCoordinates.y, loadRequest.Lod);
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

            var texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
            texture.LoadImage(materialDataKeyAndTexturePair.Value);


            inProgressMaterialData.DiffuseTex = texture;

            _materialDataCache[materialDataKey] = inProgressMaterialData;

            // Move forward dependent requests that wanted this material data
            yield return StartCoroutine(SucceedGetMaterialDataRequests(materialDataKey));
        }

        // Used to create a and populate a game object for this request 
        private IEnumerator BuildCubeRequest(LoadCubeRequest loadRequest)
        {
            Build(loadRequest.GeometryBuffer, loadRequest.MaterialData, loadRequest.X, loadRequest.Y, loadRequest.Z,
                loadRequest.Lod, loadRequest.RegisterCreatedObjects);
            DebugLog("-LoadCube(L{3}:{0}_{1}_{2})", loadRequest.X, loadRequest.Y, loadRequest.Z, loadRequest.Lod);
            yield break;
        }

        private void Build(GeometryBuffer buffer, MaterialData materialData, int x, int y, int z, int lod,
            Action<GameObject> registerCreatedObjects)
        {
            if (!_materialCache.ContainsKey(materialData.Name))
            {
                _materialCache[materialData.Name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, materialData);
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