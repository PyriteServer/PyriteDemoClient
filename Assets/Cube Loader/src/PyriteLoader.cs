namespace Assets.Cube_Loader.src
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Extensions;
    using Microsoft.Xna.Framework;
    using Model;
    using RestSharp;
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    using System.Net;

    public class PyriteLoader : MonoBehaviour
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private readonly Color[] _colorList =
        {
            Color.gray, Color.yellow, Color.cyan
        };

        private readonly DictionaryCache<string, GeometryBuffer> _eboCache = new DictionaryCache<string, GeometryBuffer>(200);
        private readonly DictionaryCache<string, Material> _materialCache = new DictionaryCache<string, Material>(100);

        private readonly DictionaryCache<string, MaterialData> _materialDataCache =
            new DictionaryCache<string, MaterialData>(100);

        private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        // Debug Text Counters
        private GUIStyle _guiStyle = new GUIStyle();
        private int EboCacheHits = 0;
        private int EboCacheMisses = 0;
        private int MaterialCacheHits = 0;
        private int MaterialCacheMisses = 0;

        private int FileCacheHits = 0;
        private int FileCacheMisses = 0;
        // End counter bits

		private const int RETRY_LIMIT = 2;
		private int _colorSelector;

		private float _geometryBufferAltitudeTransform;

        public GameObject CameraRig;
        
        public bool EnableDebugLogs = false;
        public GameObject PlaceHolderCube;

		[Header("Server Options")]
        public string PyriteServer;
		[Range(0, 100)] 
		public int MaxConcurrentRequests = 8;

		[Header("Set Options (required)")]
		public int DetailLevel = 6;
		public string ModelVersion = "V2";
        public string SetName;

		[Header("Debug Options")]
        public bool UseCameraDetection = true;
        public bool UseUnlitShader = true;
        public bool UseFileCache = true;
		public bool ShowDebugText = true;

        private readonly HashSet<string> _activeRequests = new HashSet<string>();

        private readonly Queue<LoadCubeRequest> _loadCubeRequests = new Queue<LoadCubeRequest>();

        private IEnumerator StartRequest(string path)
        {
            if (MaxConcurrentRequests == 0)
            {
                // Not limiting requests
                yield break;
            }
            while (_activeRequests.Count >= MaxConcurrentRequests)
            {
                yield return null;
            }
            _activeRequests.Add(path);
        }

        private void EndRequest(string path)
        {
            if (MaxConcurrentRequests == 0)
            {
                // Not limiting requests
                return;
            }
            _activeRequests.Remove(path);
        }

        protected void DebugLog(string fmt, params object[] args)
        {
            if (EnableDebugLogs)
            {
                var content = string.Format(fmt, args);
                Debug.LogFormat("{0}: {1}", _sw.ElapsedMilliseconds, content);
            }
        }

        private void LogResponseError(IRestResponse response, string path = "")
        {
            if (response == null)
            {
                Debug.LogErrorFormat("Response is null [{0}]", path);
                return;
            }

            if (!string.IsNullOrEmpty(response.ErrorMessage))
            {
                Debug.LogErrorFormat("Response.ErrorMessage: {0} [{1}]", response.ErrorMessage, path);
            }

            if (response.ErrorException != null)
            {
                Debug.LogErrorFormat("Response.ErrorException: {0} [{1}]", response.ErrorException, path);
            }
        }

        private void LogWwwError(WWW www, string path)
        {
            if (www == null)
            {
                Debug.LogErrorFormat("WWW is null [{0}]", path);
                return;
            }

            if (!string.IsNullOrEmpty(www.error))
            {
                Debug.LogErrorFormat("WWW.error: {0} [{1}]", www.error, path);
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

            _guiStyle.normal.textColor = Color.red;

            DebugLog("+Start()");
            StartCoroutine(Load());
            DebugLog("-Start()");
        }

        private void Update()
        {
            while (_loadCubeRequests.Count > 0)
            {
                var request = _loadCubeRequests.Dequeue();
                if (!request.Cancelled)
                {
                    StartCoroutine(ProcessLoadCubeRequest(request));
                }
            }
        }

        void OnGUI()
        {
            if (ShowDebugText)  // or check the app debug flag
            {
                if (EboCacheHits + EboCacheMisses > 1000)
                {
                    EboCacheMisses = EboCacheHits = 0;
                }

                if (MaterialCacheHits + MaterialCacheMisses > 1000)
                {
                    MaterialCacheMisses = MaterialCacheHits = 0;
                }

                int yOffset = 10;
				string caches;

				if (UseFileCache)
				{
				caches = string.Format("Mesh {0}/{1} Mat {2}/{3} File {4}/{5}", 
				                              EboCacheHits, 
				                              EboCacheMisses, 
				                              MaterialCacheHits,
				                              MaterialCacheMisses,
				                              FileCacheHits,
				                              FileCacheMisses);
				}
				else
				{
					caches = string.Format("Mesh {0}/{1} Mat {2}/{3}", 
					                       EboCacheHits, 
					                       EboCacheMisses, 
					                       MaterialCacheHits,
					                       MaterialCacheMisses);
				}

                GUI.Label(new Rect(10,yOffset,200,50), caches, _guiStyle);
            }
        }

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

            var pyriteQuery = new PyriteQuery(this, SetName, ModelVersion, PyriteServer);
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
                    var g =
                        (GameObject)
							Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + _geometryBufferAltitudeTransform, -cubePos.y),
                                Quaternion.identity);

                    g.transform.parent = gameObject.transform;
                    g.GetComponent<MeshRenderer>().material.color = _colorList[_colorSelector%_colorList.Length];
                    g.GetComponent<IsRendered>().SetCubePosition(x, y, z, DetailLevel, pyriteQuery, this);

                    g.transform.localScale = new Vector3(
                        pyriteLevel.WorldCubeScale.x,
                        pyriteLevel.WorldCubeScale.z,
                        pyriteLevel.WorldCubeScale.y);
                    _colorSelector++;
                }
                else
                {
                    var loadRequest = new LoadCubeRequest(x, y, z, DetailLevel, pyriteQuery, null);
                    EnqueueLoadCubeRequest(loadRequest);
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
                var g =
                    (GameObject)
                        Instantiate(PlaceHolderCube, new Vector3(-cubePos.x, cubePos.z + 600, -cubePos.y),
                            Quaternion.identity);

                g.transform.parent = gameObject.transform;
                g.GetComponent<MeshRenderer>().material.color = _colorList[_colorSelector%_colorList.Length];
                g.GetComponent<IsRendered>().SetCubePosition(newCube.X, newCube.Y, newCube.Z, newLod, pyriteQuery, this);

                g.transform.localScale = new Vector3(
                    pyriteLevel.WorldCubeScale.x,
                    pyriteLevel.WorldCubeScale.z,
                    pyriteLevel.WorldCubeScale.y);
                _colorSelector++;
                createdDetectors.Add(g);
            }
            registerCreatedDetectorCubes(createdDetectors);
            yield break;
        }

        public void EnqueueLoadCubeRequest(LoadCubeRequest loadRequest)
        {
            _loadCubeRequests.Enqueue(loadRequest);
        }

        private IEnumerator ProcessLoadCubeRequest(LoadCubeRequest loadRequest)
        {
            DebugLog("+LoadCube(L{3}:{0}_{1}_{2})", loadRequest.X, loadRequest.Y, loadRequest.Z, loadRequest.Lod);
            var modelPath = loadRequest.Query.GetModelPath(loadRequest.Lod, loadRequest.X, loadRequest.Y, loadRequest.Z);
            var pyriteLevel =
                loadRequest.Query.DetailLevels[loadRequest.Lod];

            GeometryBuffer buffer;

            WWW loader;
            if (!_eboCache.ContainsKey(modelPath))
            {
                EboCacheMisses++;

                _eboCache[modelPath] = null;
                bool getReturned = false;
                bool getFailed = false;
                int retryCount = 0;
                do
                {
                    yield return StartCoroutine(StartRequest(modelPath));
                    if (UseFileCache)
                    {
                        CacheWebRequest.GetBytes(modelPath, (modelResponse) =>
                        {
                            getFailed = modelResponse.IsError;
                            if (modelResponse.IsError)
                            {
                                Debug.LogError("Error getting model [" + modelPath + "] " + modelResponse.ErrorMessage);
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
								_eboCache[modelPath] = new GeometryBuffer(_geometryBufferAltitudeTransform, true) {Buffer = modelResponse.Content};
                            }

                            // Signal to waiting routine that request has returned
                            getReturned = true;
                        });
                    }
                    else
                    {
                        var client = new RestClient(modelPath);
                        var request = new RestRequest(Method.GET);
                        client.ExecuteAsync(request, (r, h) =>
                        {
                            LogResponseError(r, modelPath);
                            if (r.RawBytes != null)
                            {
								_eboCache[modelPath] = new GeometryBuffer(_geometryBufferAltitudeTransform, true) {Buffer = r.RawBytes};
                            }
                            else
                            {
                                getFailed = true;
                                _eboCache.Remove(modelPath);
                                Debug.LogError("Error getting model data");
                            }
                            getReturned = true;
                        });
                    }

                    while (!getReturned)
                    {
                        yield return null;
                    }

                    EndRequest(modelPath);
                } while (!getFailed && retryCount++ < RETRY_LIMIT);

                if (RETRY_LIMIT > retryCount)
                {
                    Debug.LogError("Retry limit hit for: " + modelPath);
                    Debug.LogError("Cube load failed for " + loadRequest);
                    yield break;
                }

            }
            else
            {
                EboCacheHits++;
            }
            // Loop while other tasks finish getting ebo data
            while (_eboCache[modelPath] == null)
            {
                // Another task is in the process of filling out this cache entry.
                // Loop until it is set
                yield return null;
            }

            // Set the geometrybuffer
            buffer = _eboCache[modelPath];


            var textureCoordinates = pyriteLevel.TextureCoordinatesForCube(loadRequest.X, loadRequest.Y);
            var materialDataKey = string.Format("model.mtl_{0}_{1}_{2}", textureCoordinates.x, textureCoordinates.y,
                loadRequest.Lod);
            if (!_materialDataCache.ContainsKey(materialDataKey))
            {
                // Cache counter
                MaterialCacheMisses++;

                var materialData = CubeBuilderHelpers.GetDefaultMaterialData((int)textureCoordinates.x, (int)textureCoordinates.y, loadRequest.Lod);
                _materialDataCache[materialDataKey] = null;
                
                
                var texturePath = loadRequest.Query.GetTexturePath(loadRequest.Query.GetLodKey(loadRequest.Lod),
                    (int) textureCoordinates.x,
                    (int) textureCoordinates.y);
                if (!_textureCache.ContainsKey(texturePath))
                {
                    // Set to null to signal to other tasks that the key is in the process
                    // of being filled
                    _textureCache[texturePath] = null;
                    byte[] textureData = null;
                    bool getReturned = false;
                    bool getFailed = false;
                    int retryCount = 0;
                    do
                    {

                        yield return StartCoroutine(StartRequest(texturePath));
                        if (UseFileCache)
                        {
                            CacheWebRequest.GetBytes(texturePath, (textureResponse) =>
                            {
                                getFailed = textureResponse.IsError;
                                if (textureResponse.IsError)
                                {
                                    Debug.LogError("Error getting texture [" + texturePath + "] " +
                                                   textureResponse.ErrorMessage);
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
                                    textureData = textureResponse.Content;
                                }
                                getReturned = true;
                            });
                        }
                        else
                        {
                            var client = new RestClient(texturePath);
                            var request = new RestRequest(Method.GET);
                            client.ExecuteAsync(request, (r, h) =>
                            {
                                LogResponseError(r, texturePath);
                                if (r.RawBytes != null)
                                {
                                    textureData = r.RawBytes;
                                }
                                else
                                {
                                    getFailed = true;
                                    Debug.LogError("Error getting texture data");
                                }
                                getReturned = true;
                            });
                        }


                        while (!getReturned)
                        {
                            yield return null;
                        }

                        var texture = new Texture2D(1, 1, TextureFormat.DXT1, false);
                        texture.LoadImage(textureData);
                        _textureCache[texturePath] = texture;
                        EndRequest(texturePath);
                    } while (!getFailed && retryCount++ < RETRY_LIMIT);

                    if (RETRY_LIMIT > retryCount)
                    {
                        Debug.LogError("Retry limit hit for: " + modelPath);
                        Debug.LogError("Cube load failed for " + loadRequest);
                        yield break;
                    }
                }

                // Loop while other tasks finish creating texture
                while (_textureCache[texturePath] == null)
                {
                    // Another task is in the process of filling out this cache entry.
                    // Loop until it is set
                    yield return null;
                }
                materialData.DiffuseTex = _textureCache[texturePath];
                
                _materialDataCache[materialDataKey] = materialData;
            }
            else
            {
                MaterialCacheHits++;
            }
            while (_materialDataCache[materialDataKey] == null)
            {
                // Another task is in the process of filling out this cache entry.
                // Loop until it is set
                yield return null;
            }
            Build(buffer, _materialDataCache[materialDataKey], loadRequest.X, loadRequest.Y, loadRequest.Z,
                loadRequest.Lod, loadRequest.RegisterCreatedObjects);
            DebugLog("-LoadCube(L{3}:{0}_{1}_{2})", loadRequest.X, loadRequest.Y, loadRequest.Z, loadRequest.Lod);
        }

        private void Build(GeometryBuffer buffer, MaterialData materialData, int x, int y, int z, int lod,
            Action<GameObject> registerCreatedObjects)
        {
           
            if (!_materialCache.ContainsKey(materialData.Name))
            {
                _materialCache[materialData.Name] = CubeBuilderHelpers.GetMaterial(UseUnlitShader, materialData);
            }           
           

            var gameObject = new GameObject();
            gameObject.name = String.Format("cube_L{3}:{0}_{1}_{2}", x, y, z, lod);
            gameObject.transform.parent = gameObject.transform;
            gameObject.AddComponent(typeof (MeshFilter));
            gameObject.AddComponent(typeof (MeshRenderer));

            if (registerCreatedObjects != null)
            {
                registerCreatedObjects(gameObject);
            }

            buffer.PopulateMeshes(gameObject, _materialCache[materialData.Name]);
        }
    }
}
